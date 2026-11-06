using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass, TestCategory("Browser"), TestCategory("PdfBrowser")]
public class PdfEdgeTests
{
    private string root = "";
    private string? originalTmp;
    private string? originalTemp;

    [TestInitialize]
    public void Initialize()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("These regressions exercise installed Windows Edge and Windows file sharing.");

        root = Path.Combine(Path.GetTempPath(), "pdf edge " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        foreach (var relative in new[] { "layout_print.html", "layout/sys_head_print.html", "admin/reports/common/pdf.html" })
        {
            var target = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(TestHelpers.TemplateRoot(), relative), target);
        }
        Directory.CreateDirectory(Path.Combine(root, "report"));
        originalTmp = Environment.GetEnvironmentVariable("TMP");
        originalTemp = Environment.GetEnvironmentVariable("TEMP");
        // Keep every framework stale-file sweep and Edge headless profile inside this disposable root.
        Environment.SetEnvironmentVariable("TMP", root);
        Environment.SetEnvironmentVariable("TEMP", root);
        Assert.AreEqual(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (root.Length == 0)
            return;

        Environment.SetEnvironmentVariable("TMP", originalTmp);
        Environment.SetEnvironmentVariable("TEMP", originalTemp);
        try
        {
            Directory.Delete(root, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DownloadCleanupIgnoresLocksAndRemovesOlderLeftovers(bool isDownloadHeldOpen)
    {
        using var body = new MemoryStream();
        var response = new HoldingPdfResponse(body, isDownloadHeldOpen);
        var context = TestHelpers.CreateHttpContext("");
        context.Features.Set<IHttpResponseBodyFeature>(response);
        using var scope = new FwTestScope(_ => new RejectingDb(), context: context);

        try
        {
            scope.Fw.config()["template"] = root;
            File.WriteAllText(Path.Combine(root, "report", "main.html"), "<p>Download cleanup</p>");
            var stale = Utils.getTmpFilename() + ".pdf";
            var locked = Utils.getTmpFilename() + ".pdf";
            File.WriteAllText(stale, "older interrupted download");
            File.WriteAllText(locked, "older locked download");
            File.SetCreationTime(stale, DateTime.Now.AddHours(-2));
            File.SetCreationTime(locked, DateTime.Now.AddHours(-2));

            using (var held = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                ConvUtils.parsePagePdf(scope.Fw, "/report", "/layout_print.html", [], "report");

                StringAssert.StartsWith(Encoding.ASCII.GetString(body.ToArray()), "%PDF-");
                Assert.AreEqual(isDownloadHeldOpen, File.Exists(response.Filename));
                Assert.IsFalse(File.Exists(stale), "The download must sweep old files left by an earlier crash.");
                Assert.IsTrue(File.Exists(locked), "A locked old file must not fail the download.");
            }

            response.HeldFile?.Dispose();
            if (isDownloadHeldOpen)
                File.SetCreationTime(response.Filename, DateTime.Now.AddHours(-2));

            Utils.cleanupTmpFiles();
            Assert.IsFalse(File.Exists(locked));
            Assert.IsFalse(File.Exists(response.Filename), "A later sweep must retry the previously locked download.");
        }
        finally
        {
            response.HeldFile?.Dispose();
        }
    }

    [TestMethod]
    public void CompletedDownloadSurvivesUnavailableCleanupDirectory()
    {
        using var body = new MemoryStream();
        var context = TestHelpers.CreateHttpContext("");
        var unavailableTmp = Path.Combine(root, "unavailable");
        File.WriteAllText(unavailableTmp, "A file cannot be used as a temporary directory.");
        context.Features.Set<IHttpResponseBodyFeature>(new HoldingPdfResponse(body, false, () =>
        {
            Environment.SetEnvironmentVariable("TMP", unavailableTmp);
            Environment.SetEnvironmentVariable("TEMP", unavailableTmp);
        }));
        using var scope = new FwTestScope(_ => new RejectingDb(), context: context);
        scope.Fw.config()["template"] = root;
        File.WriteAllText(Path.Combine(root, "report", "main.html"), "<p>Successful download</p>");

        ConvUtils.parsePagePdf(scope.Fw, "/report", "/layout_print.html", [], "report");

        StringAssert.StartsWith(Encoding.ASCII.GetString(body.ToArray()), "%PDF-");
    }

    private sealed class HoldingPdfResponse(Stream body, bool isHeldOpen, Action? afterSend = null) : StreamResponseBodyFeature(body)
    {
        internal string Filename = "";
        internal FileStream? HeldFile;

        public override async Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken)
        {
            Filename = path;
            if (isHeldOpen)
                HeldFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            await base.SendFileAsync(path, offset, count, cancellationToken);
            afterSend?.Invoke();
        }
    }

    [TestMethod]
    public async Task RendererWritesExistingDestinationInPlace()
    {
        var output = Path.Combine(root, "report.pdf");
        using var scope = new FwTestScope(_ => new RejectingDb());
        scope.Fw.config()["template"] = root;

        File.WriteAllText(output, "previous report");
        // Existing readers permit writes but do not grant delete/replace access on Windows.
        using var reader = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await ConvUtils.html2pdf(scope.Fw, "<!doctype html><html><head><title>Report</title></head><body><p>Report</p></body></html>", output);
        var signature = new byte[5];
        reader.ReadExactly(signature);
        Assert.AreEqual("%PDF-", Encoding.ASCII.GetString(signature), "Existing readers must see the updated file.");
        Assert.IsEmpty(Directory.GetFiles(Utils.getTmpDir()), "The converter must remove its staging HTML and PDF.");
    }

    [TestMethod]
    public async Task StaticReportsSupportLocalImagesPageCssAndConcurrentPrinting()
    {
        var image = Path.Combine(root, "local image.png");
        File.WriteAllBytes(image, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAE0lEQVR4nGMUqTjBAANMcBZeDgBCuAFcAjt/SgAAAABJRU5ErkJggg=="));
        File.WriteAllText(Path.Combine(root, "report", "main.html"), "<h1>Static report</h1><img src='<~image_url>'><div style='break-before:page'>Second page</div><script>document.body.innerHTML='';</script>");
        var portrait = Path.Combine(root, "portrait.pdf");
        var landscape = Path.Combine(root, "landscape.pdf");

        void print(string output, FwDict options)
        {
            using var scope = new FwTestScope(_ => new RejectingDb());
            scope.Fw.config()["template"] = root;
            ConvUtils.parsePagePdf(scope.Fw, "/report", "/layout_print.html",
                new FwDict { ["image_url"] = new Uri(image).AbsoluteUri }, output, options);
        }

        await Task.WhenAll(
            Task.Run(() => print(portrait, [])),
            Task.Run(() => print(landscape, new FwDict { ["landscape"] = true, ["margin_top"] = "20mm", ["scale"] = 1f })));

        foreach (var filename in new[] { portrait, landscape })
        {
            var pdf = Encoding.Latin1.GetString(File.ReadAllBytes(filename));
            StringAssert.StartsWith(pdf, "%PDF-");
            Assert.AreEqual(2, Regex.Matches(pdf, @"/Type\s*/Page\b").Count, "Explicit page breaks survive and template JavaScript is disabled.");
            StringAssert.Contains(pdf, filename == portrait ? "/MediaBox [0 0 612 792]" : "/MediaBox [0 0 792 612]");
            StringAssert.Contains(pdf, "/Marked true");
            StringAssert.Contains(pdf, "/Subtype /Image", "The local PNG must be embedded in the PDF.");
        }
    }

    [TestMethod]
    public void ReportRenderUsesStandardPrintLayout()
    {
        Directory.CreateDirectory(Path.Combine(root, "admin", "reports", "sample"));
        using var scope = new FwTestScope(_ => new RejectingDb(), context: TestHelpers.CreateHttpContext(""));
        File.WriteAllText(Path.Combine(root, "admin", "reports", "sample", "main.html"), "<h1>Report output</h1><div style='break-before:page'>Second page</div>");
        scope.Fw.config()["template"] = root;
        var report = new FwReportsBase();
        report.init(scope.Fw, "sample", new FwDict { ["format"] = "pdf" });
        report.render_to = Path.Combine(root, "report.pdf");
        report.render();
        var pdf = Encoding.Latin1.GetString(File.ReadAllBytes(report.render_to));
        Assert.AreEqual(2, Regex.Matches(pdf, @"/Type\s*/Page\b").Count);
        StringAssert.Contains(pdf, "/MediaBox [0 0 792 612]", "Report defaults remain landscape Letter.");
    }

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SampleReportPrintsLocalStylesLogoAndMultiplePagesWithoutWebsiteAccess()
    {
        var output = Path.Combine(TestContext.TestRunDirectory!, "sample-activity.pdf");
        var log = Path.Combine(TestContext.TestRunDirectory!, "sample-pdf-debug.log");
        var database = new SampleRowsDb();
        using var scope = new FwTestScope(_ => database, context: TestHelpers.CreateHttpContext("unreachable.invalid"));
        scope.Fw.config()["site_root"] = Path.Combine(TestHelpers.RepoRoot(), "osafw-app");
        scope.Fw.config()["template"] = TestHelpers.TemplateRoot();
        scope.Fw.G["ROOT_DOMAIN"] = "https://unreachable.invalid";
        scope.Fw.G["ASSETS_URL"] = "/assets";
        scope.Fw.flogger.log_level = LogLevel.DEBUG;
        scope.Fw.flogger.log_file = log;
        var report = new SampleReport();
        report.init(scope.Fw, "sample", new FwDict { ["format"] = "pdf" });
        report.getData();
        report.render_to = output;
        report.render();

        StringAssert.Contains(database.Sql.ToLowerInvariant(), "top 250");
        var pdf = Encoding.Latin1.GetString(File.ReadAllBytes(output));
        Assert.IsGreaterThan(1, Regex.Matches(pdf, @"/Type\s*/Page\b").Count);
        StringAssert.Contains(pdf, "/Subtype /Image", "The real site logo must be embedded.");
        Assert.IsFalse(pdf.Contains("TimesNewRoman"), "Bootstrap's system font must replace the unstyled default serif.");
        using (var reader = new StreamReader(new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
        {
            var debug = reader.ReadToEnd();
            StringAssert.Contains(debug, "PDF command:");
            StringAssert.Contains(debug, "--print-to-pdf=");
        }
        TestContext.AddResultFile(output);
        TestContext.AddResultFile(log);
    }

    [TestMethod]
    public void DeveloperDocumentationUsesTheSharedPdfPrintLayout()
    {
        using var scope = new FwTestScope(_ => new SampleRowsDb(), context: TestHelpers.CreateHttpContext("unreachable.invalid"));
        scope.Fw.config()["site_root"] = Path.Combine(TestHelpers.RepoRoot(), "osafw-app");
        scope.Fw.config()["template"] = TestHelpers.TemplateRoot();
        scope.Fw.G["ROOT_DOMAIN"] = "https://unreachable.invalid";
        var output = Path.Combine(TestContext.TestRunDirectory!, "developer-documentation.pdf");
        var html = ConvUtils.parsePagePdf(scope.Fw, "/dev/manage/docs", "/layout_print.html", [], output);

        StringAssert.Contains(html, "Content-Security-Policy");
        var pdf = Encoding.Latin1.GetString(File.ReadAllBytes(output));
        Assert.IsGreaterThan(5, Regex.Matches(pdf, @"/Type\s*/Page\b").Count);
        StringAssert.Contains(pdf, "/MediaBox [0 0 612 792]");
        TestContext.AddResultFile(output);
    }

    private sealed class SampleRowsDb : RejectingDb
    {
        public string Sql = "";
        public override DBList arrayp(string sql, FwDict? parameters = null)
        {
            if (sql.Equals("select * from [settings]", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }
            StringAssert.Contains(sql, "from activity_logs");
            Sql = sql;
            var rows = new DBList();
            for (var i = 1; i <= 160; i++)
            {
                rows.Add(new DBRow
                {
                    ["id"] = i.ToString(), ["idate"] = "2026-11-05 12:00:00",
                    ["event_name"] = "Record Updated", ["entity_name"] = "Users", ["item_id"] = i.ToString(),
                    ["idesc"] = "Synthetic test row " + i, ["payload"] = "{}", ["fname"] = "Example", ["lname"] = "User"
                });
            }
            return rows;
        }
    }

}
