using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass]
public class PdfLocalAssetTests
{
    [TestMethod]
    public async Task PolicyAllowsStaticAssetsAndRejectsExternalEscapedAndOversizedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "pdf-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "style.css"), "body { color: red; }");
            var assets = new PdfLocalAssets(root);
            var result = await assets.read(PdfLocalAssets.Origin + "/style.css?v=1");
            Assert.AreEqual("text/css", result.ContentType);
            StringAssert.Contains(Encoding.UTF8.GetString(result.Body), "color: red");
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => assets.read("https://example.test/style.css"));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => assets.read("file:///style.css"));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => assets.read(PdfLocalAssets.Origin + "/..%2foutside.css"));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => assets.read(PdfLocalAssets.Origin + "/style.css%3astream"));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => assets.read(PdfLocalAssets.Origin + "/config.json"));
            await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => assets.read(PdfLocalAssets.Origin + "/missing.png"));
            using (var large = File.Create(Path.Combine(root, "large.png"))) large.SetLength(10 * 1024 * 1024 + 1);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => assets.read(PdfLocalAssets.Origin + "/large.png"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod, TestCategory("PdfBrowser")]
    public async Task ReportRenderSelectsLocalLayoutAndUsesConfiguredAssets()
    {
        using var playwright = await Playwright.CreateAsync();
        if (!File.Exists(playwright.Chromium.ExecutablePath))
            Assert.Inconclusive("Install matching Chromium for PDF integration tests.");
        var root = Path.Combine(Path.GetTempPath(), "pdf-report-" + Guid.NewGuid().ToString("N"));
        var templates = Path.Combine(root, "templates");
        var assets = Path.Combine(root, "assets");
        Directory.CreateDirectory(Path.Combine(templates, "admin", "reports", "local"));
        Directory.CreateDirectory(Path.Combine(assets, "lib", "bootstrap", "css"));
        Directory.CreateDirectory(Path.Combine(assets, "css"));
        using var scope = new FwTestScope(_ => new RejectingDb(), context: TestHelpers.CreateHttpContext(""));
        try
        {
            File.Copy(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../osafw-app/App_Data/template/layout_print_local.html")), Path.Combine(templates, "layout_print_local.html"));
            File.WriteAllText(Path.Combine(templates, "layout_print.html"), "<img src='/wrong-layout.png'>");
            File.WriteAllText(Path.Combine(templates, "admin", "reports", "local", "main.html"), "<p>Report output</p>");
            File.WriteAllText(Path.Combine(assets, "lib", "bootstrap", "css", "bootstrap.min.css"), "body { color: black; }");
            File.WriteAllText(Path.Combine(assets, "css", "site.css"), "p { font-size: 14pt; }");
            scope.Fw.config()["template"] = templates;
            var report = new FwReportsBase();
            report.init(scope.Fw, "local", new FwDict { ["format"] = "pdf" });
            report.render_to = Path.Combine(root, "report.pdf");
            report.render_options["local_assets_root"] = assets;
            report.render();
            Assert.IsTrue(File.Exists(report.render_to));
            Assert.IsEmpty(Directory.GetFiles(root, "*.tmp"));
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod, TestCategory("PdfBrowser")]
    public async Task RendererLoadsLocalStylesAndImagesWithoutExternalConnectionsAndPreservesOutputOnFailure()
    {
        using var playwright = await Playwright.CreateAsync();
        if (!File.Exists(playwright.Chromium.ExecutablePath))
            Assert.Inconclusive("Install matching Chromium with playwright.ps1 install chromium --no-shell to run PDF integration tests.");
        var root = Path.Combine(Path.GetTempPath(), "pdf-render-" + Guid.NewGuid().ToString("N"));
        var assets = Path.Combine(root, "assets");
        Directory.CreateDirectory(assets);
        using var scope = new FwTestScope(_ => new RejectingDb());
        var output = Path.Combine(root, "report.pdf");
        try
        {
            File.WriteAllText(Path.Combine(assets, "style.css"), "body { color: #123456; } img { width: 20px; }");
            File.WriteAllText(Path.Combine(assets, "mark.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"red\"/></svg>");
            var options = new FwDict { ["local_assets_root"] = assets };
            await ConvUtils.html2pdf(scope.Fw, "<html><head><link rel='stylesheet' href='/style.css'></head><body>Local report<img loading='lazy' src='/mark.svg'></body></html>", output, options);
            var previous = File.ReadAllBytes(output);
            StringAssert.StartsWith(Encoding.ASCII.GetString(previous, 0, 5), "%PDF-");
            Assert.IsGreaterThan(500, previous.Length);
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ConvUtils.html2pdf(scope.Fw,
                $"<img src='http://127.0.0.1:{port}/mark.svg'>", output, options));
            Assert.IsFalse(listener.Pending(), "The renderer must not connect to a rejected URL.");
            CollectionAssert.AreEqual(previous, File.ReadAllBytes(output));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ConvUtils.html2pdf(scope.Fw,
                "<link rel='stylesheet' href='file:///does-not-exist.css'><p>Report</p>", output, options));
            CollectionAssert.AreEqual(previous, File.ReadAllBytes(output));
            File.WriteAllText(Path.Combine(assets, "broken.png"), "not image data");
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ConvUtils.html2pdf(scope.Fw,
                "<style>body { background-image: url(/broken.png); }</style>Report", output, options));
            CollectionAssert.AreEqual(previous, File.ReadAllBytes(output));
            await ConvUtils.html2pdf(scope.Fw, "<style>body { background-image: url(/mark.svg); }</style>Report", output, options);
            previous = File.ReadAllBytes(output);
            var pages = Regex.Matches(Encoding.Latin1.GetString(previous), @"/Type\s*/Page\b").Count;
            await ConvUtils.html2pdf(scope.Fw,
                "<style>body { background-image:url(/mark.svg); } img { display:block !important; width:100px; height:1800px; }</style>Report", output, options);
            previous = File.ReadAllBytes(output);
            Assert.AreEqual(pages, Regex.Matches(Encoding.Latin1.GetString(previous), @"/Type\s*/Page\b").Count, "Diagnostic images must not affect PDF layout.");
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ConvUtils.html2pdf(scope.Fw, "<img src='/missing.png'>", output, options));
            CollectionAssert.AreEqual(previous, File.ReadAllBytes(output));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ConvUtils.html2pdf(scope.Fw, "<style>@font-face { font-family: LocalMissing; src: url(/missing.woff2); } body { font-family: LocalMissing; }</style>Font check", output, options));
            Assert.IsEmpty(Directory.GetFiles(root, "*.tmp"));
            await ConvUtils.html2pdf(scope.Fw, "<p>Legacy rendering still works.</p>", Path.Combine(root, "legacy.pdf"));
            Assert.IsTrue(File.Exists(Path.Combine(root, "legacy.pdf")));
        }
        finally { Directory.Delete(root, true); }
    }
}
