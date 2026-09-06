#if isSQLite
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace osafw.Tests;

[TestClass]
public partial class SpagesCmsTests
{
    private string path = "";
    private IConfiguration config = null!;
    private readonly List<FW> requests = [];
    private FW fw = null!;
    private Spages cms = null!;
    private bool sqlServer;

    private static string root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "osafw-app"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private FW request(int level = 100)
    {
        var context = TestHelpers.CreateHttpContext("spages-tests");
        var current = new FW(context, config);
        if (level > 0) { current.Session("user_id", "1"); current.Session("access_level", level.ToString()); }
        requests.Add(current);
        return current;
    }

    [TestInitialize]
    public void init()
    {
        path = Path.Combine(Path.GetTempPath(), "osafw-spages-" + Guid.NewGuid().ToString("N") + ".sqlite");
        string sqlConnection = Environment.GetEnvironmentVariable("SPAGES_CMS_TEST_SQLSERVER") ?? "";
        sqlServer = sqlConnection.Length > 0;
        if (sqlServer && new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sqlConnection).InitialCatalog != "osafw_spages_cms_verify_20260905") throw new InvalidOperationException("CMS integration tests require the explicitly approved disposable SQL Server database.");
        config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["appSettings:db:main:type"] = sqlServer ? "SQL" : "SQLite", ["appSettings:db:main:timezone"] = "UTC",
            ["appSettings:db:main:connection_string"] = sqlServer ? sqlConnection : "Data Source=" + path + ";Pooling=False;Foreign Keys=True",
            ["appSettings:SPAGES_ENABLED"] = "true", ["appSettings:SPAGES_AUTHOR_LEVEL"] = "80", ["appSettings:SPAGES_PUBLISHER_LEVEL"] = "90",
            ["appSettings:SPAGES_PUBLIC_ORIGIN"] = "https://example.org",
            ["appSettings:ROOT_DOMAIN"] = "https://spages-tests",
            ["appSettings:site_root"] = Path.Combine(root(), "osafw-app")
        }).Build();
        fw = request();
        if (sqlServer)
        {
            // Fresh scripts initialize an empty schema. Reset foreign keys only in this explicitly named disposable database.
            Assert.AreEqual("osafw_spages_cms_verify_20260905", fw.db.valuep("SELECT DB_NAME()").toStr());
            foreach (var key in fw.db.arrayp("SELECT name, OBJECT_SCHEMA_NAME(parent_object_id) AS schema_name, OBJECT_NAME(parent_object_id) AS table_name FROM sys.foreign_keys"))
                fw.db.exec("ALTER TABLE " + fw.db.qid(key["schema_name"].toStr()) + "." + fw.db.qid(key["table_name"].toStr()) + " DROP CONSTRAINT " + fw.db.qid(key["name"].toStr()));
        }
        string sqlRoot = Path.Combine(root(), "osafw-app/App_Data/sql", sqlServer ? "" : "sqlite");
        string baseline = Environment.GetEnvironmentVariable("SPAGES_CMS_TEST_BASELINE") ?? "";
        if (baseline.Length > 0) fw.db.execMultipleSQL("DROP TABLE IF EXISTS spages_revisions;\nDROP TABLE IF EXISTS spages_redirects;");
        fw.db.execMultipleSQL(File.ReadAllText(baseline.Length > 0 ? baseline : Path.Combine(sqlRoot, "fwdatabase.sql")));
        if (baseline.Length > 0)
        {
            // Exercise provider discovery and its ledger, applying only this task's update to the old schema.
            bool logging = fw.is_log_events;
            fw.is_log_events = false;
            try
            {
                var updates = fw.model<FwUpdates>();
                updates.loadUpdates();
                var update = fw.db.row("fwupdates", DB.h("iname", "upd2026-09-05-spages-cms.sql"));
                Assert.AreEqual(File.ReadAllText(Path.Combine(sqlRoot, "updates/upd2026-09-05-spages-cms.sql")), update["idesc"].toStr());
                updates.applyOne(update["id"].toInt());
                updates.loadUpdates();
                Assert.AreEqual(FwUpdates.STATUS_APPLIED, fw.db.value("fwupdates", DB.h("id", update["id"]), "status").toInt());
                Assert.AreEqual(1, fw.db.array("fwupdates", DB.h("iname", "upd2026-09-05-spages-cms.sql")).Count);
            }
            finally { fw.is_log_events = logging; }
        }
        cms = fw.model<Spages>();
        cms.migrateLegacy();
    }

    [TestCleanup]
    public void cleanup()
    {
        foreach (var current in requests) current.db.disconnect();
        requests.Clear();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
    }

    private static string content(string text, string? snippet = null)
    {
        var doc = SpagesContent.empty();
        var blocks = (JsonArray)doc["regions"]!["main"]!["blocks"]!;
        blocks.Add(new JsonObject { ["type"] = "paragraph", ["data"] = new JsonObject { ["text"] = text } });
        if (snippet != null) blocks.Add(new JsonObject { ["type"] = "snippet", ["data"] = new JsonObject { ["key"] = snippet } });
        return doc.ToJsonString();
    }
    private int create(string slug = "sample", int parent = 0, int access = 0, string? snippet = null) => cms.saveDraft(0, DB.h("iname", "Sample", "url", slug, "template", "article", "content_json", content("Original public text", snippet), "nav_visible", 1, "parent_id", parent, "access_level", access), 0);
    private int publish(int id, DateTime? date = null) => cms.transition(id, cms.draft(id)["edit_version"].toInt(), "publish", publishAt: date);
    private void save(int id, string text) => cms.saveDraft(id, DB.h("content_json", content(text)), cms.draft(id)["edit_version"].toInt());

    [TestMethod]
    public void LegacyReadsAndMarkdownTemplatesUseCurrentPublicationWithoutDraftLeakage()
    {
        int id = create(); publish(id);
        save(id, "Latest <strong>approved</strong> text"); publish(id);
        save(id, "Private next draft");
        var anonymous = request(0).model<Spages>();
        var page = anonymous.oneByFullUrl("/sample");
        var parser = new ParsePage(null!);
        string html = parser.parse_string("<~page[idesc] markdown noescape>", DB.h("page", page));
        StringAssert.Contains(html, "<strong>approved</strong>");
        Assert.IsFalse(html.Contains("Private next draft"));
        StringAssert.Contains(parser.parse_string("<~page[idesc] markdown>", DB.h("page", page)), "&lt;strong&gt;");
        Assert.AreEqual(page["idesc"].toStr(), anonymous.one(id)["idesc"].toStr());
        Assert.AreEqual(page["idesc"].toStr(), anonymous.oneByUrl("sample", 0)["idesc"].toStr());
        cms.saveDraft(id, DB.h("access_level", 80), cms.draft(id)["edit_version"].toInt()); publish(id);
        anonymous = request(0).model<Spages>();
        Assert.AreEqual(0, anonymous.one(id).Count);
        Assert.AreEqual("", anonymous.getFullUrl(id));
        Assert.IsFalse(anonymous.isPublished(page));
    }

    [TestMethod]
    public void DraftsAreIsolatedAndConflictingInputCannotReplaceStoredDraft()
    {
        int id = create();
        Assert.AreEqual(0, cms.published(id, 0).Count);
        int version = cms.draft(id)["edit_version"].toInt();
        cms.saveDraft(id, DB.h("content_json", content("First editor")), version, true);
        Assert.ThrowsExactly<SpagesConflictException>(() => cms.saveDraft(id, DB.h("content_json", content("Second editor")), version));
        StringAssert.Contains(cms.draft(id)["content_json"].toStr(), "First editor");
        Assert.AreEqual(1, cms.history(id).Count, "Autosaves must coalesce without history growth.");
        publish(id);
        save(id, "Secret working draft");
        var publicCms = request(0).model<Spages>();
        StringAssert.Contains(publicCms.publishedText(publicCms.published(id)), "First editor");
        Assert.IsFalse(publicCms.publishedText(publicCms.published(id)).Contains("Secret working draft"));
    }

    [TestMethod]
    public void ScheduledPublicationLeavesLiveRevisionAndWithdrawalDoesNotRevealOldContent()
    {
        int id = create();
        publish(id);
        save(id, "Future content");
        DateTime boundary = DateTime.UtcNow.AddHours(1);
        publish(id, boundary);
        StringAssert.Contains(cms.publishedText(cms.published(id)), "Original public text");
        StringAssert.Contains(cms.publications(boundary.AddSeconds(1))[id]["content_json"].toStr(), "Future content");
        cms.transition(id, cms.draft(id)["edit_version"].toInt(), "cancel");
        StringAssert.Contains(cms.publications(boundary.AddHours(1))[id]["content_json"].toStr(), "Original public text");
        cms.transition(id, cms.draft(id)["edit_version"].toInt(), "unpublish");
        Assert.AreEqual(0, request(0).model<Spages>().published(id).Count);
        Assert.IsFalse(cms.publications(boundary.AddHours(2)).ContainsKey(id));
    }

    [TestMethod]
    public void AuthorCanSubmitAndPreviewButCannotPublishOrReadAnotherPagesRevision()
    {
        int id = create(), other = create("other");
        var author = request(80).model<Spages>();
        author.transition(id, 1, "submit", "Ready for review");
        Assert.AreEqual("in_review", author.draft(id)["workflow"]);
        Assert.ThrowsExactly<AuthException>(() => author.transition(id, 2, "publish"));
        Assert.ThrowsExactly<AuthException>(() => request(1).model<Spages>().requireAuthor());
        Assert.ThrowsExactly<NotFoundException>(() => author.revision(other, author.history(id)[0]["id"].toInt()));
        var preview = author.pageState(author.draft(id), true);
        Assert.IsTrue(preview["is_preview"].toBool());
        Assert.IsTrue(preview["noindex"].toBool());
    }

    [TestMethod]
    public void RollbackRestoresDraftAndPreservesPublicationUntilRepublished()
    {
        int id = create();
        int original = publish(id);
        save(id, "Replacement"); publish(id);
        cms.restoreRevision(id, original, cms.draft(id)["edit_version"].toInt());
        StringAssert.Contains(cms.publishedText(cms.published(id)), "Replacement");
        publish(id);
        StringAssert.Contains(cms.publishedText(cms.published(id)), "Original public text");
        StringAssert.Contains(cms.revision(id, original)["content_json"].toStr(), "Original public text");
    }

    [TestMethod]
    public void AncestorAccessGatesRoutesNavigationAndPageOwnedFiles()
    {
        int parent = create("internal", access: 50); publish(parent);
        int child = create("child", parent); publish(child);
        var anonymous = request(0).model<Spages>();
        Assert.AreEqual(0, anonymous.publishedByPath("/internal/child").Count);
        Assert.IsFalse(anonymous.publishedPages().Any(x => x["id"].toInt() == child));
        Assert.IsFalse(anonymous.isAttachmentVisible(123, child));
        Assert.AreEqual(child, request(50).model<Spages>().publishedByPath("/internal/child")["id"].toInt());
        int publicPage = create("public"); publish(publicPage);
        Assert.IsFalse(anonymous.isAttachmentVisible(123, publicPage), "Unreferenced uploads must not become public with their parent page.");
    }

    [TestMethod]
    public void SnippetUpdatesPropagateAndHistoricalPreviewPinsTheApprovedDependency()
    {
        int snippet = cms.saveDraft(0, DB.h("iname", "Shared help", "is_snippet", 1, "snippet_key", "help", "template", "article", "content_json", content("Old help")), 0);
        int firstSnippetRevision = publish(snippet);
        int page = create(snippet: "help"); int pageRevision = publish(page);
        save(snippet, "New help"); publish(snippet);
        StringAssert.Contains(cms.publishedText(cms.published(page)), "New help");
        var historical = cms.pageState(cms.revision(page, pageRevision), true, true);
        StringAssert.Contains(((FwDict)historical["page"]!)["html_main"].toStr(), "Old help");
        Assert.ThrowsExactly<UserException>(() => cms.transition(snippet, cms.draft(snippet)["edit_version"].toInt(), "unpublish"));
        Assert.AreEqual(0, request(0).model<Spages>().publishedByPath("/help").Count);
        cms.restoreRevision(snippet, firstSnippetRevision, cms.draft(snippet)["edit_version"].toInt()); publish(snippet);
        StringAssert.Contains(cms.publishedText(cms.published(page)), "Old help");
    }

    [TestMethod]
    public void MissingNestedAndMoreRestrictedSnippetsCannotPublish()
    {
        int page = create(snippet: "missing");
        Assert.ThrowsExactly<UserException>(() => publish(page));
        Assert.ThrowsExactly<UserException>(() => cms.saveDraft(0, DB.h("iname", "Nested", "is_snippet", 1, "snippet_key", "nested", "template", "article", "content_json", content("", "missing")), 0));
        Assert.ThrowsExactly<UserException>(() => cms.saveDraft(0, DB.h("iname", "Sidebar", "is_snippet", 1, "snippet_key", "sidebar", "template", "sidebar-right", "content_json", content("Help")), 0));
        int snippet = cms.saveDraft(0, DB.h("iname", "Help", "is_snippet", 1, "snippet_key", "missing", "template", "article", "content_json", content("Secret"), "access_level", 50), 0); publish(snippet);
        Assert.ThrowsExactly<UserException>(() => publish(page));
    }

    [TestMethod]
    public void ScheduledParentRenameRedirectsDescendantsAtTheSameBoundary()
    {
        int parent = create("old"); publish(parent);
        int child = create("child", parent); publish(child);
        cms.saveDraft(parent, DB.h("url", "new"), cms.draft(parent)["edit_version"].toInt());
        DateTime boundary = DateTime.UtcNow.AddHours(1); publish(parent, boundary);
        Assert.AreEqual("/old/child", cms.publishedUrl(child));
        Assert.AreEqual("/new/child", cms.publishedUrl(child, cms.publications(boundary.AddSeconds(1))));
        Assert.AreEqual("", cms.resolveRedirect("/old/child"));
        var redirects = fw.db.array(Spages.RedirectTable, DB.h("source_url", "/old/child"));
        Assert.AreEqual(1, redirects.Count);
        Assert.AreEqual(child, redirects[0]["spages_id"].toInt());
    }

    [TestMethod]
    public void UrlCollisionReservedRoutesAndRedirectLoopsAreRejected()
    {
        int page = create("used"); publish(page);
        int duplicate = create("used"); Assert.ThrowsExactly<UserException>(() => publish(duplicate));
        int admin = create("Admin"); Assert.ThrowsExactly<UserException>(() => publish(admin));
        cms.saveRedirect("/first", "/second");
        Assert.ThrowsExactly<UserException>(() => cms.saveRedirect("/second", "/first"));
        Assert.ThrowsExactly<UserException>(() => cms.saveRedirect("/out", "https://elsewhere.test/"));
        Assert.ThrowsExactly<UserException>(() => cms.saveRedirect("/used", "/elsewhere"));
    }

    [TestMethod]
    public void LegacyMigrationAndSampleInstallationAreRepeatable()
    {
        Assert.AreEqual(0, cms.migrateLegacy());
        Assert.AreEqual(2, cms.history(2).Count);
        Assert.AreEqual("original", cms.history(2)[1]["kind"]);
        var first = cms.installSamples("services"); var second = cms.installSamples("department");
        Assert.AreEqual(2, first.Count);
        Assert.IsTrue(first.All(x => x["created"].toBool()));
        Assert.AreEqual(2, second.Count);
        Assert.AreEqual(1, second.Count(x => x["created"].toBool()));
        Assert.IsTrue(cms.installSamples().All(x => !x["created"].toBool()));
        Assert.IsTrue(first.All(x => cms.published(x["id"].toInt()).Count == 0));
    }

    [TestMethod]
    public void LaterScheduledCollisionAndSnippetAncestorWithdrawalAreRejected()
    {
        int first = create("first"); publish(first);
        cms.saveDraft(first, DB.h("url", "reserved-later"), cms.draft(first)["edit_version"].toInt()); publish(first, DateTime.UtcNow.AddDays(2));
        int second = create("reserved-later");
        Assert.ThrowsExactly<UserException>(() => publish(second, DateTime.UtcNow.AddDays(1)));
        int parent = create("department"); publish(parent);
        int snippet = cms.saveDraft(0, DB.h("iname", "Department help", "is_snippet", 1, "snippet_key", "department-help", "parent_id", parent, "template", "article", "content_json", content("Help")), 0); publish(snippet);
        int consumer = create("consumer", snippet: "department-help"); publish(consumer);
        Assert.ThrowsExactly<UserException>(() => cms.transition(parent, cms.draft(parent)["edit_version"].toInt(), "unpublish"));
    }

    [TestMethod]
    public void LegacyConversionPreservesFutureDateCustomFieldsAndColumns()
    {
        DateTime date = DateTime.UtcNow.AddDays(3);
        const string original = "## Introduction\n\nUseful **text**.\n\n- A\n- B\n\n<div>Unsupported markup</div>";
        int id = fw.db.insert("spages", DB.h("iname", "Migration", "url", "migration", "idesc", original, "idesc_left", "Left content", "idesc_right", "Right content", "custom_css", ".demo { color: red; }", "pub_time", date, "status", 0));
        Assert.AreEqual(1, cms.migrateLegacy());
        var draft = cms.draft(id);
        Assert.AreEqual("three-column", draft["template"]);
        Assert.AreEqual(original, draft["idesc"]);
        Assert.AreEqual(".demo { color: red; }", draft["custom_css"]);
        Assert.AreEqual(0, cms.published(id).Count);
        Assert.IsTrue(cms.publications(date.AddSeconds(1)).ContainsKey(id));
        Assert.AreEqual(0, cms.migrateLegacy());
        var doc = SpagesContent.parse(draft["content_json"].toStr());
        StringAssert.Contains(SpagesContent.renderRegion(doc, "left", new()), "Left content");
        StringAssert.Contains(SpagesContent.renderRegion(doc, "right", new()), "Right content");
        Assert.AreEqual(original, cms.revision(id, cms.history(id).Last()["id"].toInt())["idesc"]);
    }

    [TestMethod]
    public void FirstEditBeforeConversionCannotWithdrawOrLoseOriginalAndDeletedRowsRemainConvertible()
    {
        int id = fw.db.insert("spages", DB.h("iname", "Before conversion", "url", "before-conversion", "idesc", "Original source", "status", 0));
        Assert.ThrowsExactly<UserException>(() => cms.saveDraft(id, DB.h("iname", "New draft"), 0, true));
        foreach (string action in new[] { "submit", "publish", "cancel", "changes", "unpublish" })
            Assert.ThrowsExactly<UserException>(() => cms.transition(id, 0, action, "Review note"));
        Assert.AreEqual(0, fw.db.value("spages", DB.h("id", id), "edit_version").toInt());
        StringAssert.Contains(request(0).model<Spages>().oneByFullUrl("/before-conversion")["idesc"].toStr(), "Original source");
        Assert.AreEqual(0, cms.history(id).Count);
        Assert.AreEqual(1, cms.migrateLegacy());
        cms.saveDraft(id, DB.h("iname", "New draft"), cms.draft(id)["edit_version"].toInt());
        Assert.AreEqual("Original source", cms.revision(id, cms.history(id).Last()["id"].toInt())["idesc"]);
        int deleted = fw.db.insert("spages", DB.h("iname", "Deleted original", "url", "deleted-original", "idesc", "Recoverable source", "status", 127));
        var controller = new AdminSpagesController(); controller.init(fw);
        Assert.IsTrue(controller.IndexAction()!["needs_upgrade"].toBool());
        Assert.AreEqual(1, cms.migrateLegacy());
        Assert.AreEqual("Recoverable source", cms.revision(deleted, cms.history(deleted).Last()["id"].toInt())["idesc"]);
        Assert.AreEqual(0, cms.migrateLegacy());
    }

    [TestMethod]
    public void RedirectGraphUsesCaseInsensitivePageIdentity()
    {
        int first = create("first");
        cms.saveDraft(first, DB.h("redirect_url", "/second"), cms.draft(first)["edit_version"].toInt()); publish(first);
        int second = create("second");
        cms.saveDraft(second, DB.h("redirect_url", "/FIRST"), cms.draft(second)["edit_version"].toInt());
        Assert.ThrowsExactly<UserException>(() => publish(second));
        Assert.AreEqual(0, request(0).model<Spages>().published(second).Count);
        cms.saveRedirect("/old-name", "/first");
        Assert.AreEqual("/first", request(0).model<Spages>().resolveRedirect("/OLD-NAME"));
        Assert.ThrowsExactly<UserException>(() => cms.saveRedirect("/second", "/OLD-NAME"));
    }
}
#endif
