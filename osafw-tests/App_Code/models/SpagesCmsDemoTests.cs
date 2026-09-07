#if isSQLite
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace osafw.Tests;

public partial class SpagesCmsTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow("original")]
    [DataRow("thumbnail")]
    public void DemoInitializationRendersEveryLayoutAndBlockWithLocalMedia(string occupied)
    {
        string appRoot = Path.Combine(Path.GetTempPath(), "osafw-cms-demo-" + Guid.NewGuid().ToString("N"));
        string provider = isSqlServer ? "" : "sqlite";
        string sqlRoot = Path.Combine(appRoot, "App_Data", "sql", provider);
        string demoRoot = Path.Combine(appRoot, "App_Data", "demo");
        Directory.CreateDirectory(sqlRoot);
        Directory.CreateDirectory(demoRoot);
        foreach (string name in new[] { "fwdatabase.sql", "spages.sql", "database.sql", "demo.sql", "lookups.sql", "views.sql" })
        {
            string source = Path.Combine(root(), "osafw-app", "App_Data", "sql", provider, name);
            if (File.Exists(source))
                File.Copy(source, Path.Combine(sqlRoot, name));
        }
        foreach (string source in Directory.EnumerateFiles(Path.Combine(root(), "osafw-app", "App_Data", "demo")))
            File.Copy(source, Path.Combine(demoRoot, Path.GetFileName(source)));

        config = new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["appSettings:site_root"] = appRoot,
            ["appSettings:UPLOAD_DIR"] = "/uploads",
            ["appSettings:IS_DEV"] = "true",
            ["appSettings:db:main:connection_string"] = isSqlServer
                ? config["appSettings:db:main:connection_string"]
                : "Data Source=" + Path.Combine(appRoot, "demo.db") + ";Pooling=False;Foreign Keys=True"
        }).Build();
        try
        {
            var current = request();
            current.is_log_events = false;
            current.route.method = "POST";
            addXssToken(current, "demo-init-token");
            var controller = new DevConfigureController();
            controller.init(current);
            // Fresh attachment IDs start at one, but files from an earlier database may still occupy that path.
            if (occupied.Length > 0)
            {
                string extension = occupied == "original" ? "1.png" : "1_s.png";
                string existing = Path.Combine(appRoot, "uploads", "att", "1", extension);
                Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
                File.WriteAllText(existing, "Unrelated private upload bytes");
                Assert.ThrowsExactly<UserException>(() => controller.InitDBAction());
                Assert.AreEqual("Unrelated private upload bytes", File.ReadAllText(existing));
                var pending = current.db.row("att", DB.h("icode", "demo-spages-workshop"));
                Assert.AreEqual(FwModel.STATUS_INACTIVE, pending["status"].toInt());
                Assert.ThrowsExactly<AuthException>(() => request(0).model<Att>().checkAccess(pending["id"].toInt()));
                return;
            }
            controller.InitDBAction(); // Real bootstrap SQL and bundled-file installation in an isolated app root/database.
            cms = request().model<Spages>();
            var pages = current.db.arrayp("SELECT * FROM spages WHERE url LIKE 'demo-%'");
            Assert.HasCount(6, pages);
            var layouts = new HashSet<string>();
            var blocks = new HashSet<string>();
            var attachments = new HashSet<int>();
            foreach (var page in pages)
            {
                Assert.AreEqual(Spages.STATUS_DRAFT, page["status"].toInt());
                Assert.IsFalse(page["is_home"].toBool());
                Assert.AreEqual(page["content_json"].toStr(), cms.oneDraftOrFail(page["id"].toInt())["content_json"].toStr());
                Assert.IsEmpty(request(0).model<Spages>().onePublished(page["id"].toInt()));
                var doc = SpagesContent.parse(page["content_json"].toStr());
                if (!page["is_snippet"].toBool())
                    layouts.Add(page["template"].toStr());
                foreach (var region in ((JsonObject)doc["regions"]!).Select(x => x.Value))
                {
                    foreach (var block in ((JsonArray)region!["blocks"]!).OfType<JsonObject>())
                        blocks.Add(block["type"]!.ToString());
                }
                attachments.UnionWith(SpagesContent.listAttachmentIds(doc));
            }
            CollectionAssert.AreEquivalent(SpagesContent.layouts().Keys.ToArray(), layouts.ToArray());
            CollectionAssert.AreEquivalent(new[] { "paragraph", "header", "list", "quote", "image", "file", "table", "code", "delimiter", "callout", "cards", "button", "snippet", "legacyMarkdown" }, blocks.ToArray());
            Assert.HasCount(2, attachments);
            foreach (int id in attachments)
            {
                var att = current.model<Att>();
                var row = att.one(id);
                string source = Path.Combine(demoRoot, row["fname"].toStr());
                string stored = att.getUploadImgPath(id, "", row["ext"].toStr());
                CollectionAssert.AreEqual(File.ReadAllBytes(source), File.ReadAllBytes(stored));
                Assert.AreEqual(new FileInfo(stored).Length, row["fsize"].toLong());
                if (row["is_image"].toBool())
                {
                    foreach (string size in new[] { "s", "m", "l" })
                        Assert.IsTrue(File.Exists(att.getUploadImgPath(id, size, row["ext"].toStr())));
                }
            }
            publish(pages.Single(x => x["is_snippet"].toBool())["id"].toInt());
            foreach (var page in pages.Where(x => !x["is_snippet"].toBool()))
            {
                publish(page["id"].toInt());
                var rendered = renderCmsResponse("/" + page["url"].toStr());
                Assert.AreEqual(200, rendered.Current.response.StatusCode);
                StringAssert.Contains(rendered.Html, "spage-layout-" + page["template"].toStr());
                Assert.IsFalse(rendered.Html.Contains("Snippet unavailable"));
                Assert.IsFalse(rendered.Html.Contains("@demo-spages-"));
                if (page["template"].toStr() == "article")
                {
                    StringAssert.Contains(rendered.Html, "<figure><img");
                    StringAssert.Contains(rendered.Html, "<pre><code>");
                    StringAssert.Contains(rendered.Html, "Before you share the brief");
                }
                if (page["template"].toStr() == "sidebar-left")
                    StringAssert.Contains(rendered.Html, "Download the first-week checklist (plain text)");
            }
        }
        finally
        {
            foreach (var current in requests)
                current.db.disconnect();
            Directory.Delete(appRoot, true);
        }
    }
}
#endif
