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
    private bool isSqlServer;
    private bool isBaseline;
    private static string root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "osafw-app")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private FW request(int level = 100, string pathBase = "")
    {
        var context = TestHelpers.CreateHttpContext("spages-tests");
        context.Request.PathBase = pathBase;
        var current = new FW(context, config);
        if (level > 0)
        {
            current.Session("user_id", "1");
            current.Session("access_level", level.ToString());
        }

        requests.Add(current);
        return current;
    }

    [TestInitialize]
    public void init()
    {
        path = Path.Combine(Path.GetTempPath(), "osafw-spages-" + Guid.NewGuid().ToString("N") + ".sqlite");
        string sqlConnection = Environment.GetEnvironmentVariable("SPAGES_CMS_TEST_SQLSERVER") ?? "";
        isSqlServer = sqlConnection.Length > 0;
        if (isSqlServer && new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sqlConnection).InitialCatalog != "osafw_spages_cms_verify_20260905")
        {
            throw new InvalidOperationException("CMS integration tests require the explicitly approved disposable SQL Server database.");
        }

        config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["appSettings:db:main:type"] = isSqlServer ? "SQL" : "SQLite",
            ["appSettings:db:main:timezone"] = isSqlServer ? null : "UTC",
            ["appSettings:db:main:connection_string"] = isSqlServer ? sqlConnection : "Data Source=" + path + ";Pooling=False;Foreign Keys=True",
            ["appSettings:ROOT_DOMAIN"] = "https://example.org",
            ["appSettings:site_root"] = Path.Combine(root(), "osafw-app"),
            ["appSettings:template"] = Path.Combine(root(), "osafw-app/App_Data/template")
        }).Build();
        fw = request();
        if (isSqlServer)
        {
            // Fresh scripts initialize an empty schema. Reset foreign keys only in this explicitly named disposable database.
            Assert.AreEqual("osafw_spages_cms_verify_20260905", fw.db.valuep("SELECT DB_NAME()").toStr());
            foreach (var key in fw.db.arrayp("SELECT name, OBJECT_SCHEMA_NAME(parent_object_id) AS schema_name, OBJECT_NAME(parent_object_id) AS table_name FROM sys.foreign_keys"))
            {
                fw.db.exec("ALTER TABLE " + fw.db.qid(key["schema_name"].toStr()) + "." + fw.db.qid(key["table_name"].toStr()) + " DROP CONSTRAINT " + fw.db.qid(key["name"].toStr()));
            }
        }

        string sqlRoot = Path.Combine(root(), "osafw-app/App_Data/sql", isSqlServer ? "" : "sqlite");
        string baseline = Environment.GetEnvironmentVariable("SPAGES_CMS_TEST_BASELINE") ?? "";
        isBaseline = baseline.Length > 0;
        if (isBaseline)
        {
            fw.db.execMultipleSQL("DROP TABLE IF EXISTS spages_revisions;");
        }

        fw.db.execMultipleSQL(File.ReadAllText(baseline.Length > 0 ? baseline : Path.Combine(sqlRoot, "fwdatabase.sql")));
        if (baseline.Length == 0)
        {
            fw.db.execMultipleSQL(File.ReadAllText(Path.Combine(sqlRoot, "spages.sql")));
        }

        if (baseline.Length > 0)
        {
            // Exercise provider discovery and its ledger, applying only this task's update to the old schema.
            bool isLoggingEvents = fw.is_log_events;
            fw.is_log_events = false;
            try
            {
                var updates = fw.model<FwUpdates>();
                updates.loadUpdates();
                var update = fw.db.row("fwupdates", new FwDict { ["iname"] = "upd2026-09-05-spages-cms.sql" });
                Assert.AreEqual(File.ReadAllText(Path.Combine(sqlRoot, "updates/upd2026-09-05-spages-cms.sql")), update["idesc"].toStr());
                updates.applyOne(update["id"].toInt());
                var statusUpdate = fw.db.row("fwupdates", new FwDict { ["iname"] = "upd2026-09-06-spages-status.sql" });
                updates.applyOne(statusUpdate["id"].toInt());
                var metadataUpdate = fw.db.row("fwupdates", new FwDict { ["iname"] = "upd2026-09-06-spages-working-metadata.sql" });
                updates.applyOne(metadataUpdate["id"].toInt());
                updates.loadUpdates();
                Assert.AreEqual(FwUpdates.STATUS_APPLIED, fw.db.value("fwupdates", new FwDict { ["id"] = update["id"] }, "status").toInt());
                Assert.AreEqual(1, fw.db.array("fwupdates", new FwDict { ["iname"] = "upd2026-09-05-spages-cms.sql" }).Count);
            }
            finally
            {
                fw.is_log_events = isLoggingEvents;
            }
        }

#if isRoles
        loadRolesSchema();
#endif
        cms = fw.model<Spages>();
        if (isBaseline)
        {
            migrate();
        }
    }

    [TestCleanup]
    public void cleanup()
    {
        foreach (var current in requests)
        {
            current.db.disconnect();
        }

        requests.Clear();
        foreach (var suffix in new[]
        {
            "",
            "-wal",
            "-shm"
        }

        )
        {
            if (File.Exists(path + suffix))
            {
                File.Delete(path + suffix);
            }
        }
    }

    private static string content(string text, string? snippet = null)
    {
        var doc = SpagesContent.empty();
        var blocks = (JsonArray)doc["regions"]!["main"]!["blocks"]!;
        blocks.Add(new JsonObject { ["type"] = "paragraph", ["data"] = new JsonObject { ["text"] = text } });
        if (snippet != null)
        {
            blocks.Add(new JsonObject { ["type"] = "snippet", ["data"] = new JsonObject { ["key"] = snippet } });
        }

        return doc.ToJsonString();
    }

    private int create(string slug = "sample", int parent = 0, int access = 0, string? snippet = null) => cms.saveDraft(0, new FwDict
    {
        ["iname"] = "Sample",
        ["url"] = slug,
        ["template"] = "article",
        ["content_json"] = content("Original public text", snippet),
        ["is_nav_visible"] = 1,
        ["parent_id"] = parent,
        ["access_level"] = access
    });

    private int publish(int id, DateTime? date = null) => cms.updateWorkflow(id, "publish", publishAt: date);

    private void save(int id, string text) => cms.saveDraft(id, new FwDict { ["content_json"] = content(text) });

    private AdminSpagesController migrationController(FW current, string method = "POST", bool isTokenIncluded = true)
    {
        current.setController("AdminSpages", "Migrate");
        current.route.method = method;
        current.Session("XSS", "cms-test-token");
        if (isTokenIncluded)
        {
            current.FORM["XSS"] = "cms-test-token";
        }

        var controller = new AdminSpagesController();
        controller.init(current);
        return controller;
    }

    private int migrate()
    {
        var controller = migrationController(fw);
        controller.checkAccess();
        var result = (FwDict)controller.MigrateAction()["_json"]!;
        Assert.IsTrue(result["success"].toBool());
        return result["count"].toInt();
    }

    private (FW Current, string Html) renderCmsResponse(string pagePath, int level = 0, string pathBase = "")
    {
        var current = request(level, pathBase);
        current.G["PAGE_LAYOUT"] = "main.html";
        using var body = new MemoryStream();
        current.response.Body = body;
        current.model<Spages>().showCmsPage(pagePath);
        body.Position = 0;
        return (current, new StreamReader(body).ReadToEnd());
    }

    private int[] searchCmsPages(string marker)
    {
        var current = request(0);
        current.FORM["s"] = marker;
        current.setController("Search", "Index");
        var controller = new SearchController();
        controller.init(current);
        controller.checkAccess();
        var state = controller.IndexAction();
        return ((FwList)state["results"]!).Select(page => page["id"].toInt()).ToArray();
    }

    [TestMethod]
    public void SearchTemplateShowsMatchingCountInsteadOfBooleanState()
    {
        int page = create("search-summary");
        publish(page);
        var current = request(0);
        current.FORM["s"] = "Original public text";
        current.setController("Search", "Index");
        var controller = new SearchController();
        controller.init(current);
        var parser = new ParsePage(new ParsePageOptions { TemplatesRoot = Path.Combine(root(), "osafw-app/App_Data/template"), IsLangUpdate = false });
        string html = parser.parse_page("/search/index", "main.html", controller.IndexAction());
        StringAssert.Contains(html, "1 matching pages");
        StringAssert.Contains(html, "search-summary");
        Assert.IsFalse(html.Contains(">True<"));
    }

    [TestMethod]
    [DataRow("length", false)]
    [DataRow("depth", false)]
    [DataRow("length", true)]
    [DataRow("depth", true)]
    public void ParentMovesCannotMakePublishedOrScheduledDescendantsUnroutable(string limit, bool isChildScheduled)
    {
        string parentSlug = limit == "length" ? new string ('p', 200) : "movable";
        string childSlug = limit == "length" ? new string ('c', 200) : "child";
        int parent = create(parentSlug);
        publish(parent);
        int child = create(childSlug, parent);
        DateTime boundary = DateTime.UtcNow.AddDays(1);
        publish(child, isChildScheduled ? boundary : null);
        string originalPath = "/" + parentSlug + "/" + childSlug;
        int validAncestor;
        int invalidAncestor;
        if (limit == "length")
        {
            validAncestor = create(new string ('a', 20));
            publish(validAncestor);
            invalidAncestor = create(new string ('b', 100));
            publish(invalidAncestor);
        }
        else
        {
            validAncestor = 0;
            for (int depth = 1; depth <= 18; depth++)
            {
                validAncestor = create("level-" + depth, validAncestor);
                publish(validAncestor);
            }

            invalidAncestor = create("level-19", validAncestor);
            publish(invalidAncestor);
        }

        cms.saveDraft(parent, new FwDict { ["parent_id"] = invalidAncestor });
        Assert.ThrowsExactly<UserException>(() => publish(parent));
        Assert.ThrowsExactly<UserException>(() => publish(parent, boundary.AddHours(1)));
        var afterRejection = request(0).model<Spages>();
        var originalView = afterRejection.listPublicationsByDate(boundary.AddHours(2));
        Assert.AreEqual(originalPath, afterRejection.publishedUrl(child, originalView));
        Assert.IsTrue(afterRejection.isVisible(originalView[child], originalView, 0));
        Assert.AreEqual(0, afterRejection.onePublished(parent)["parent_id"].toInt());
        cms.saveDraft(parent, new FwDict { ["parent_id"] = validAncestor });
        publish(parent);
        var validView = cms.listPublicationsByDate(boundary.AddHours(2));
        string validPath = cms.publishedUrl(child, validView);
        Assert.AreEqual(validPath, Spages.localPath(validPath));
        Assert.IsTrue(cms.isVisible(validView[child], validView, 0));
        if (!isChildScheduled)
        {
            Assert.AreEqual(child, request(0).model<Spages>().onePublishedByPath(validPath)["id"].toInt());
        }

        cms.updateWorkflow(parent, "unpublish");
        var withdrawnView = cms.listPublicationsByDate(boundary.AddHours(2));
        Assert.IsFalse(cms.isVisible(withdrawnView[child], withdrawnView, 0), "Withdrawing an ancestor intentionally hides its published or scheduled descendants.");
    }

    [TestMethod]
    public void HiddenNavigationChildRemainsDirectlyRoutableAndSearchable()
    {
        const string MARKER = "navigation-visibility-marker";
        int parent = create("navigation-parent");
        publish(parent);
        int visible = create("visible-child", parent);
        save(visible, MARKER + " visible");
        publish(visible);
        int hidden = create("hidden-child", parent);
        cms.saveDraft(hidden, new FwDict { ["content_json"] = content(MARKER + " hidden"), ["is_nav_visible"] = 0 });
        publish(hidden);
        var reader = request(0).model<Spages>();
        var state = reader.buildPageState(reader.onePublished(parent));
        CollectionAssert.AreEquivalent(new[] { visible }, ((FwList)state["subpages"]!).Select(page => page["id"].toInt()).ToArray());
        Assert.IsFalse(((FwList)state["pages"]!).Any(page => page["id"].toInt() == hidden));
        var parentResponse = renderCmsResponse("/navigation-parent");
        StringAssert.Contains(parentResponse.Html, "href=\"/navigation-parent/visible-child\"");
        Assert.IsFalse(parentResponse.Html.Contains("href=\"/navigation-parent/hidden-child\""));
        Assert.AreEqual(200, renderCmsResponse("/navigation-parent/hidden-child").Current.response.StatusCode);
        CollectionAssert.AreEquivalent(new[] { visible, hidden }, searchCmsPages(MARKER));
    }

    [TestMethod]
    [DataRow("deleted")]
    [DataRow("inactive")]
    [DataRow("missing")]
    [DataRow("not-image")]
    [DataRow("rebound")]
    public void UnavailableMediaPreservesPublicPageSearchAndHistoricalText(string condition)
    {
        const string MARKER = "media-survival-marker";
        enableRagQueue();
        int page = create("media-survival");
        int stable = create("media-stable");
        save(stable, MARKER + " stable page");
        publish(stable);
        int entity = fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_SPAGE);
        int addMedia(string code, bool isImage)
        {
            return fw.db.insert("att", new FwDict
            {
                ["icode"] = code,
                ["fwentities_id"] = entity,
                ["item_id"] = page,
                ["iname"] = code,
                ["fname"] = code + (isImage ? ".png" : ".txt"),
                ["ext"] = isImage ? ".png" : ".txt",
                ["fsize"] = 1,
                ["is_image"] = isImage ? 1 : 0,
                ["status"] = FwModel.STATUS_ACTIVE
            });
        }

        int image = addMedia("cms-survival-image", true);
        int file = addMedia("cms-survival-file", false);
        var document = SpagesContent.parse(content(MARKER + " before media"));
        var blocks = (JsonArray)document["regions"]!["main"]!["blocks"]!;
        blocks.Add(new JsonObject { ["type"] = "image", ["data"] = new JsonObject
        {
            ["att_id"] = image,
            ["alt"] = "Published diagram",
            ["caption"] = "Published diagram caption"
        } });
        blocks.Add(new JsonObject { ["type"] = "file", ["data"] = new JsonObject { ["att_id"] = file, ["title"] = "Published file label" } });
        blocks.Add(new JsonObject { ["type"] = "paragraph", ["data"] = new JsonObject { ["text"] = MARKER + " after media" } });
        cms.saveDraft(page, new FwDict { ["head_att_id"] = image, ["content_json"] = document.ToJsonString() });
        int revision = publish(page);
        addIndexedSpageChunk(page, MARKER, [1.0f, 0.0f]);
        addIndexedSpageChunk(stable, MARKER, [0.0f, 1.0f]);
        var active = renderCmsResponse("/media-survival");
        Assert.AreEqual(200, active.Current.response.StatusCode);
        StringAssert.Contains(active.Html, "src=\"/Att/cms-survival-image\"");
        StringAssert.Contains(active.Html, "href=\"/Att/cms-survival-file\"");
        request(0).model<Att>().checkAccess(file);
        CollectionAssert.AreEquivalent(new[] { page, stable }, searchCmsPages(MARKER));
        CollectionAssert.Contains(ragResultPageIds(request(0)), page);
        if (condition == "not-image")
        {
            fw.db.update("att", new FwDict { ["is_image"] = 0 }, new FwDict { ["id"] = image });
        }
        else
        {
            foreach (int attachment in new[]
            {
                image,
                file
            }

            )
            {
                if (condition == "missing")
                {
                    // A deleted attachment can remain in historical JSON after its live foreign key is cleared.
                    fw.db.update("spages", new FwDict { ["head_att_id"] = null }, new FwDict { ["head_att_id"] = attachment });
                    fw.db.del("att", new FwDict { ["id"] = attachment });
                }
                else if (condition == "rebound")
                {
                    fw.db.update("att", new FwDict { ["item_id"] = stable }, new FwDict { ["id"] = attachment });
                }
                else
                {
                    int status = condition == "deleted" ? FwModel.STATUS_DELETED : FwModel.STATUS_INACTIVE;
                    fw.db.update("att", new FwDict { ["status"] = status }, new FwDict { ["id"] = attachment });
                }
            }
        }

        var unavailable = renderCmsResponse("/media-survival");
        Assert.AreEqual(200, unavailable.Current.response.StatusCode);
        StringAssert.Contains(unavailable.Html, MARKER + " before media");
        StringAssert.Contains(unavailable.Html, MARKER + " after media");
        Assert.IsFalse(unavailable.Html.Contains("/Att/cms-survival-image"));
        Assert.IsFalse(unavailable.Html.Contains("spage-hero"));
        Assert.AreEqual(condition == "not-image", unavailable.Html.Contains("/Att/cms-survival-file"));
        CollectionAssert.AreEquivalent(new[] { page, stable }, searchCmsPages(MARKER));
        var historical = request(80).model<Spages>();
        var historyState = historical.buildPageState(historical.oneRevisionOrFail(page, revision), true, true);
        var historicalPage = (FwDict)historyState["page"]!;
        StringAssert.Contains(historicalPage["html_main"].toStr(), MARKER + " after media");
        Assert.IsFalse(historicalPage["html_main"].toStr().Contains("/Att/cms-survival-image"));
        Assert.AreEqual("", historicalPage["head_att_id_url"].toStr());
        var retrieval = ragResultPageIds(request(0));
        CollectionAssert.Contains(retrieval, stable, "A missing attachment must not abort unrelated retrieval results.");
        CollectionAssert.DoesNotContain(retrieval, page, "Changed rendered text requires a fresh index.");
        var publisher = request().model<Spages>();
        if (condition == "rebound")
        {
            Assert.ThrowsExactly<AuthException>(() => publisher.updateWorkflow(page, "publish"));
        }
        else
        {
            Assert.ThrowsExactly<UserException>(() => publisher.updateWorkflow(page, "publish"));
        }

        Assert.AreEqual(revision, request(0).model<Spages>().onePublished(page)["revision_id"].toInt());
    }

    [TestMethod]
    public void CmsRedirectResponsesPreventCachingAndHonorAudienceAndRestoredUrls()
    {
        int page = create("cache-original");
        publish(page);
        cms.saveDraft(page, new FwDict { ["url"] = "cache-current", ["url_aliases"] = "/cache-manual" });
        publish(page);
        foreach (string alias in new[]
        {
            "/cache-original",
            "/cache-manual"
        }

        )
        {
            var anonymous = renderCmsResponse(alias);
            Assert.AreEqual(301, anonymous.Current.response.StatusCode);
            Assert.AreEqual("/cache-current", anonymous.Current.response.Headers.Location.ToString());
            Assert.AreEqual("no-store", anonymous.Current.response.Headers.CacheControl.ToString());
            var signedIn = renderCmsResponse(alias, 80);
            Assert.AreEqual(301, signedIn.Current.response.StatusCode);
            Assert.AreEqual("private, no-store", signedIn.Current.response.Headers.CacheControl.ToString());
        }

        int redirectPage = create("cache-redirect");
        cms.saveDraft(redirectPage, new FwDict { ["redirect_url"] = "/cache-current" });
        publish(redirectPage);
        var redirect = renderCmsResponse("/cache-redirect");
        Assert.AreEqual(301, redirect.Current.response.StatusCode);
        Assert.AreEqual("/cache-current", redirect.Current.response.Headers.Location.ToString());
        Assert.AreEqual("no-store", redirect.Current.response.Headers.CacheControl.ToString());
        var signedInRedirect = renderCmsResponse("/cache-redirect", 80);
        Assert.AreEqual("private, no-store", signedInRedirect.Current.response.Headers.CacheControl.ToString());
        cms.saveDraft(page, new FwDict { ["url"] = "cache-original", ["url_aliases"] = "" });
        publish(page);
        var restored = renderCmsResponse("/cache-original");
        Assert.AreEqual(200, restored.Current.response.StatusCode);
        Assert.AreEqual("", restored.Current.response.Headers.Location.ToString());
        Assert.AreEqual("no-cache", restored.Current.response.Headers.CacheControl.ToString());
        StringAssert.Contains(restored.Html, "Original public text");
        var oldCurrent = renderCmsResponse("/cache-current");
        Assert.AreEqual("/cache-original", oldCurrent.Current.response.Headers.Location.ToString());
        Assert.AreEqual("no-store", oldCurrent.Current.response.Headers.CacheControl.ToString());
        cms.saveDraft(redirectPage, new FwDict { ["redirect_url"] = "" });
        publish(redirectPage);
        Assert.AreEqual(200, renderCmsResponse("/cache-redirect").Current.response.StatusCode);
        cms.saveDraft(page, new FwDict { ["access_level"] = 80 });
        publish(page);
        var restrictedAlias = renderCmsResponse("/cache-current", 80);
        Assert.AreEqual(301, restrictedAlias.Current.response.StatusCode);
        Assert.AreEqual("private, no-store", restrictedAlias.Current.response.Headers.CacheControl.ToString());
        var denied = request(0);
        Assert.ThrowsExactly<NotFoundException>(() => denied.model<Spages>().showCmsPage("/cache-current"));
        Assert.AreEqual("", denied.response.Headers.Location.ToString());
        Assert.AreEqual("no-store", denied.response.Headers.CacheControl.ToString());
        Assert.AreEqual("private, no-store", renderCmsResponse("/cache-original", 80).Current.response.Headers.CacheControl.ToString());
    }

    [TestMethod]
    public void PublicReadsAndCmsTemplateRenderOnlySanitizedPublishedBlocks()
    {
        int id = create();
        publish(id);
        save(id, "Latest <strong>approved</strong> text<img src=x onerror=alert(1)>");
        publish(id);
        save(id, "Private next draft");
        var anonymous = request(0).model<Spages>();
        var page = anonymous.oneByFullUrl("/sample");
        var state = anonymous.buildPageState(page);
        StringAssert.Contains(((FwDict)state["page"]!)["html_main"].toStr(), "<strong>approved</strong>");
        var parser = new ParsePage(new ParsePageOptions
        {
            TemplatesRoot = Path.Combine(root(), "osafw-app/App_Data/template"),
            IsLangUpdate = false,
            GlobalsGetter = () => new FwDict { ["ROOT_URL"] = "" }
        });
        string html = parser.parse_page("/home/spage", "main.html", state);
        StringAssert.Contains(html, "<strong>approved</strong>");
        Assert.IsFalse(html.Contains("onerror") || html.Contains("Private next draft"));
        Assert.AreEqual(page["content_json"].toStr(), anonymous.one(id)["content_json"].toStr());
        Assert.AreEqual(page["content_json"].toStr(), anonymous.oneByUrl("sample", 0)["content_json"].toStr());
        cms.saveDraft(id, new FwDict { ["access_level"] = 80 });
        publish(id);
        anonymous = request(0).model<Spages>();
        Assert.AreEqual(0, anonymous.one(id).Count);
        Assert.AreEqual("", anonymous.getFullUrl(id));
        Assert.IsFalse(anonymous.isPublished(page));
    }

    [TestMethod]
    public void ExecutablePageFieldsRequireSiteAdminAndRemainIsolatedUntilPublication()
    {
        const string TRUSTED_HEAD = "<script>window.cmsHead = \"trusted\";</script>";
        const string TRUSTED_CSS = ".cms-trusted::before { content: \"<trusted>\"; }";
        const string TRUSTED_JS = "window.cmsTrusted = \"<trusted>\";";
        var trustedFields = new FwDict
        {
            ["custom_head"] = TRUSTED_HEAD,
            ["custom_css"] = TRUSTED_CSS,
            ["custom_js"] = TRUSTED_JS
        };
        int page = create("trusted-code");
        publish(page);
        cms.saveDraft(page, new FwDict
        {
            ["content_json"] = content("Site Admin working copy"),
            ["custom_head"] = TRUSTED_HEAD,
            ["custom_css"] = TRUSTED_CSS,
            ["custom_js"] = TRUSTED_JS
        });
        var beforeApproval = request(0).model<Spages>();
        var initialPublication = beforeApproval.onePublished(page);
        foreach (string field in trustedFields.Keys)
        {
            Assert.AreEqual("", initialPublication[field].toStr());
        }

        StringAssert.Contains(beforeApproval.publishedText(initialPublication), "Original public text");
        Assert.IsFalse(beforeApproval.publishedText(initialPublication).Contains("Site Admin working copy"));
        publish(page);
        var anonymous = request(0).model<Spages>();
        var approvedPage = anonymous.onePublished(page);
        var approvedState = anonymous.buildPageState(approvedPage);
        foreach (string field in trustedFields.Keys)
        {
            Assert.AreEqual(trustedFields[field], approvedPage[field]);
        }

        var parser = new ParsePage(new ParsePageOptions { TemplatesRoot = Path.Combine(root(), "osafw-app/App_Data/template"), IsLangUpdate = false });
        Assert.AreEqual(TRUSTED_HEAD, parser.parse_page("/home/spage", "head_script.html", approvedState).Trim());
        StringAssert.Contains(parser.parse_page("/home/spage", "head.css", approvedState), TRUSTED_CSS);
        Assert.AreEqual(TRUSTED_JS, parser.parse_page("/home/spage", "head.js", approvedState).Trim());
        var manager = request(80).model<Spages>();
        manager.saveDraft(page, new FwDict
        {
            ["content_json"] = content("Manager working copy"),
            ["custom_head"] = "<script>window.managerHead = true;</script>",
            ["custom_css"] = ".manager-css { color: red; }",
            ["custom_js"] = "window.managerScript = true;"
        });
        var managerDraft = manager.oneDraftOrFail(page);
        foreach (string field in trustedFields.Keys)
        {
            Assert.AreEqual(trustedFields[field], managerDraft[field]);
        }

        var duringDraft = request(0).model<Spages>();
        var livePage = duringDraft.onePublished(page);
        StringAssert.Contains(duringDraft.publishedText(livePage), "Site Admin working copy");
        Assert.IsFalse(duringDraft.publishedText(livePage).Contains("Manager working copy"));
        manager.updateWorkflow(page, "publish");
        var afterApproval = request(0).model<Spages>();
        var managerPublication = afterApproval.onePublished(page);
        StringAssert.Contains(afterApproval.publishedText(managerPublication), "Manager working copy");
        foreach (string field in trustedFields.Keys)
        {
            Assert.AreEqual(trustedFields[field], managerPublication[field]);
        }

        var managerState = afterApproval.buildPageState(managerPublication);
        Assert.AreEqual(TRUSTED_HEAD, parser.parse_page("/home/spage", "head_script.html", managerState).Trim());
        StringAssert.Contains(parser.parse_page("/home/spage", "head.css", managerState), TRUSTED_CSS);
        Assert.AreEqual(TRUSTED_JS, parser.parse_page("/home/spage", "head.js", managerState).Trim());
        var author = request(Spages.AUTHOR_LEVEL).model<Spages>();
        int authorPage = author.saveDraft(0, new FwDict
        {
            ["iname"] = "Author page",
            ["url"] = "author-code",
            ["template"] = "article",
            ["content_json"] = content("Author public copy"),
            ["custom_head"] = "<script>window.authorHead = true;</script>",
            ["custom_css"] = ".author-css { color: red; }",
            ["custom_js"] = "window.authorScript = true;"
        });
        var authorDraft = author.oneDraftOrFail(authorPage);
        foreach (string field in trustedFields.Keys)
        {
            Assert.AreEqual("", authorDraft[field].toStr());
        }

        author.updateWorkflow(authorPage, "publish");
        var publicAuthorPage = request(0).model<Spages>();
        var authorPublication = publicAuthorPage.onePublished(authorPage);
        foreach (string field in trustedFields.Keys)
        {
            Assert.AreEqual("", authorPublication[field].toStr());
        }

        var authorState = publicAuthorPage.buildPageState(authorPublication);
        Assert.AreEqual("", parser.parse_page("/home/spage", "head_script.html", authorState).Trim());
        Assert.IsFalse(parser.parse_page("/home/spage", "head.css", authorState).Contains(".author-css"));
        Assert.AreEqual("", parser.parse_page("/home/spage", "head.js", authorState).Trim());
    }

    [TestMethod]
    public void LastSaveWinsWhileAutosavesCoalesceAndPublishedContentStaysIsolated()
    {
        int id = create();
        Assert.AreEqual(0, cms.onePublished(id, 0).Count);
        cms.saveDraft(id, new FwDict { ["content_json"] = content("First editor") }, true);
        cms.saveDraft(id, new FwDict { ["content_json"] = content("Second editor") }, true);
        StringAssert.Contains(cms.oneDraftOrFail(id)["content_json"].toStr(), "Second editor");
        Assert.AreEqual(1, cms.listRevisions(id).Count, "Autosaves must not grow revision history.");
        cms.saveDraft(id, new FwDict { ["content_json"] = content("Final editor") });
        Assert.AreEqual(2, cms.listRevisions(id).Count);
        Assert.AreEqual(Spages.KIND_SAVED, cms.listRevisions(id)[0]["kind"].toInt());
        publish(id);
        save(id, "Secret working draft");
        var anonymous = request(0).model<Spages>();
        StringAssert.Contains(anonymous.publishedText(anonymous.onePublished(id)), "Final editor");
        Assert.IsFalse(anonymous.publishedText(anonymous.onePublished(id)).Contains("Secret working draft"));
    }

    [TestMethod]
    public void ScheduledPublicationLeavesLiveRevisionAndWithdrawalDoesNotRevealOldContent()
    {
        int id = create();
        publish(id);
        save(id, "Future content");
        DateTime boundary = DateTime.UtcNow.AddHours(1);
        int scheduledRevision = publish(id, boundary);
        Assert.IsTrue(cms.isScheduled(id));
        StringAssert.Contains(cms.publishedText(cms.onePublished(id)), "Original public text");
        StringAssert.Contains(cms.listPublicationsByDate(boundary.AddSeconds(1))[id]["content_json"].toStr(), "Future content");
        cms.updateWorkflow(id, "cancel");
        Assert.AreEqual(127, fw.db.value(Spages.REVISION_TABLE, new FwDict { ["id"] = scheduledRevision }, "status").toInt());
        Assert.IsFalse(cms.isScheduled(id));
        StringAssert.Contains(cms.listPublicationsByDate(boundary.AddHours(1))[id]["content_json"].toStr(), "Original public text");
        cms.updateWorkflow(id, "unpublish");
        Assert.AreEqual(0, request(0).model<Spages>().onePublished(id).Count);
        Assert.IsFalse(cms.listPublicationsByDate(boundary.AddHours(2)).ContainsKey(id));
    }

    [TestMethod]
    public void EditorialRoleCanReviewAndPublishWhileLowerAccessAndWrongRevisionAreDenied()
    {
        int id = create(), other = create("other");
        var editor = request(80).model<Spages>();
        Assert.IsTrue(editor.isAuthor());
        Assert.IsTrue(editor.isPublisher());
        editor.updateWorkflow(id, "submit", "Ready for review");
        Assert.AreEqual(Spages.STATUS_IN_REVIEW, editor.oneDraftOrFail(id)["status"].toInt());
        Assert.AreEqual(Spages.KIND_SUBMITTED, editor.listRevisions(id)[0]["kind"].toInt());
        editor.updateWorkflow(id, "changes", "Clarify the introduction");
        Assert.AreEqual(Spages.STATUS_CHANGES_REQUESTED, editor.oneDraftOrFail(id)["status"].toInt());
        editor.updateWorkflow(id, "publish");
        Assert.AreEqual(Spages.STATUS_PUBLISHED, editor.oneDraftOrFail(id)["status"].toInt());
        var denied = request(79).model<Spages>();
        Assert.IsFalse(denied.isAuthor());
        Assert.IsFalse(denied.isPublisher());
        Assert.IsFalse(denied.isPreviewAllowed());
        Assert.ThrowsExactly<AuthException>(() => denied.saveDraft(id, new FwDict { ["iname"] = "Denied" }));
        Assert.ThrowsExactly<AuthException>(() => denied.updateWorkflow(id, "publish"));
        Assert.ThrowsExactly<NotFoundException>(() => editor.oneRevisionOrFail(other, editor.listRevisions(id)[0]["id"].toInt()));
        var preview = cms.buildPageState(cms.oneDraftOrFail(id), true);
        Assert.IsTrue(preview["is_preview"].toBool());
        Assert.IsTrue(preview["is_noindex"].toBool());
    }

    [TestMethod]
    public void RollbackRestoresDraftAndPreservesPublicationUntilRepublished()
    {
        int id = create();
        int original = publish(id);
        save(id, "Replacement");
        publish(id);
        cms.restoreRevision(id, original);
        StringAssert.Contains(cms.publishedText(cms.onePublished(id)), "Replacement");
        publish(id);
        StringAssert.Contains(cms.publishedText(cms.onePublished(id)), "Original public text");
        StringAssert.Contains(cms.oneRevisionOrFail(id, original)["content_json"].toStr(), "Original public text");
    }

    [TestMethod]
    public void AncestorAccessGatesRoutesNavigationAndPageOwnedFiles()
    {
        int parent = create("internal", access: 50);
        publish(parent);
        int child = create("child", parent);
        publish(child);
        var anonymous = request(0).model<Spages>();
        Assert.AreEqual(0, anonymous.onePublishedByPath("/internal/child").Count);
        Assert.IsFalse(anonymous.listPublished().Any(x => x["id"].toInt() == child));
        Assert.IsFalse(anonymous.isAttachmentVisible(123, child));
        Assert.AreEqual(child, request(50).model<Spages>().onePublishedByPath("/internal/child")["id"].toInt());
        int publicPage = create("public");
        publish(publicPage);
        Assert.IsFalse(anonymous.isAttachmentVisible(123, publicPage), "Unreferenced uploads must not become public with their parent page.");
    }

    [TestMethod]
    public void SnippetUpdatesPropagateAndHistoricalPreviewPinsTheApprovedDependency()
    {
        int snippet = cms.saveDraft(0, new FwDict
        {
            ["iname"] = "Shared help",
            ["is_snippet"] = 1,
            ["url"] = "help",
            ["template"] = "article",
            ["content_json"] = content("Old help")
        });
        int firstSnippetRevision = publish(snippet);
        int page = create(snippet: "help");
        int pageRevision = publish(page);
        save(snippet, "New help");
        publish(snippet);
        StringAssert.Contains(cms.publishedText(cms.onePublished(page)), "New help");
        var historical = cms.buildPageState(cms.oneRevisionOrFail(page, pageRevision), true, true);
        StringAssert.Contains(((FwDict)historical["page"]!)["html_main"].toStr(), "Old help");
        Assert.ThrowsExactly<UserException>(() => cms.updateWorkflow(snippet, "unpublish"));
        Assert.AreEqual(0, request(0).model<Spages>().onePublishedByPath("/help").Count);
        cms.restoreRevision(snippet, firstSnippetRevision);
        publish(snippet);
        StringAssert.Contains(cms.publishedText(cms.onePublished(page)), "Old help");
    }

    [TestMethod]
    public void MissingNestedAndMoreRestrictedSnippetsCannotPublish()
    {
        int page = create(snippet: "missing");
        Assert.ThrowsExactly<UserException>(() => publish(page));
        Assert.ThrowsExactly<UserException>(() => cms.saveDraft(0, new FwDict
        {
            ["iname"] = "Nested",
            ["is_snippet"] = 1,
            ["url"] = "nested",
            ["template"] = "article",
            ["content_json"] = content("", "missing")
        }));
        Assert.ThrowsExactly<UserException>(() => cms.saveDraft(0, new FwDict
        {
            ["iname"] = "Sidebar",
            ["is_snippet"] = 1,
            ["url"] = "sidebar",
            ["template"] = "sidebar-right",
            ["content_json"] = content("Help")
        }));
        int snippet = cms.saveDraft(0, new FwDict
        {
            ["iname"] = "Help",
            ["is_snippet"] = 1,
            ["url"] = "missing",
            ["template"] = "article",
            ["content_json"] = content("Secret"),
            ["access_level"] = 50
        });
        publish(snippet);
        Assert.ThrowsExactly<UserException>(() => publish(page));
    }

    [TestMethod]
    public void ImmediateSameSecondUrlRenamesRetainEveryPublishedAlias()
    {
        int page = create("immediate-old");
        int originalRevision = publish(page);
        cms.saveDraft(page, new FwDict { ["url"] = "immediate-middle" });
        int middleRevision = publish(page);
        cms.saveDraft(page, new FwDict { ["url"] = "immediate-new" });
        int latestRevision = publish(page);
        // Equal whole-second timestamps represent immediate approvals, ordered by revision ID.
        DateTime approvalTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        foreach (int revisionId in new[]
        {
            originalRevision,
            middleRevision,
            latestRevision
        }

        )
        {
            fw.db.update(Spages.REVISION_TABLE, new FwDict { ["effective_time"] = approvalTime, ["add_time"] = approvalTime }, new FwDict { ["id"] = revisionId });
        }

        var anonymous = request(0).model<Spages>();
        Assert.AreEqual(page, anonymous.onePublishedByPath("/immediate-new")["id"].toInt());
        Assert.AreEqual("/immediate-new", anonymous.resolveRedirect("/immediate-old"));
        Assert.AreEqual("/immediate-new", anonymous.resolveRedirect("/immediate-middle"));
        Assert.AreEqual(0, anonymous.onePublishedByPath("/immediate-old").Count);
        Assert.AreEqual("", anonymous.resolveRedirect("/immediate-never-published"));
    }

    [TestMethod]
    public void SimultaneousScheduledParentAndChildRenamesExcludeIntermediateAliases()
    {
        int parent = create("scheduled-old");
        int originalParentRevision = publish(parent);
        int child = create("old-child", parent);
        int originalChildRevision = publish(child);
        DateTime futureBoundary = DateTime.UtcNow.AddDays(1);
        cms.saveDraft(parent, new FwDict { ["url"] = "scheduled-new" });
        int scheduledParentRevision = publish(parent, futureBoundary);
        cms.saveDraft(child, new FwDict { ["url"] = "new-child" });
        int scheduledChildRevision = publish(child, futureBoundary);
        DateTime originalTime = new DateTime(2020, 1, 2, 3, 0, 0, DateTimeKind.Utc);
        DateTime scheduledTime = originalTime.AddHours(1);
        foreach (int revisionId in new[]
        {
            originalParentRevision,
            originalChildRevision
        }

        )
        {
            fw.db.update(Spages.REVISION_TABLE, new FwDict { ["effective_time"] = originalTime, ["add_time"] = originalTime }, new FwDict { ["id"] = revisionId });
        }

        foreach (int revisionId in new[]
        {
            scheduledParentRevision,
            scheduledChildRevision
        }

        )
        {
            fw.db.update(Spages.REVISION_TABLE, new FwDict { ["effective_time"] = scheduledTime, ["add_time"] = originalTime.AddMinutes(15) }, new FwDict { ["id"] = revisionId });
        }

        var anonymous = request(0).model<Spages>();
        var before = anonymous.listPublicationsByDate(scheduledTime.AddSeconds(-1));
        var after = anonymous.listPublicationsByDate(scheduledTime);
        Assert.AreEqual("/scheduled-old/old-child", anonymous.publishedUrl(child, before));
        Assert.AreEqual("/scheduled-new/new-child", anonymous.publishedUrl(child, after));
        Assert.AreEqual(child, anonymous.onePublishedByPath("/scheduled-new/new-child")["id"].toInt());
        Assert.AreEqual("/scheduled-new", anonymous.resolveRedirect("/scheduled-old"));
        Assert.AreEqual("/scheduled-new/new-child", anonymous.resolveRedirect("/scheduled-old/old-child"));
        Assert.AreEqual("", anonymous.resolveRedirect("/scheduled-new/old-child"));
        Assert.AreEqual("", anonymous.resolveRedirect("/scheduled-old/new-child"));
        Assert.AreEqual(0, anonymous.onePublishedByPath("/scheduled-new/old-child").Count);
        Assert.AreEqual(0, anonymous.onePublishedByPath("/scheduled-old/new-child").Count);
    }

    [TestMethod]
    public void ScheduledParentRenameActivatesDescendantAliasesOnlyAtItsBoundary()
    {
        int parent = create("old");
        int originalParentRevision = publish(parent);
        int child = create("child", parent);
        int childRevision = publish(child);
        cms.saveDraft(parent, new FwDict { ["url"] = "new" });
        DateTime boundary = DateTime.UtcNow.AddHours(1);
        int scheduledRevision = publish(parent, boundary);
        Assert.IsTrue(cms.isScheduled(parent));
        Assert.AreEqual("/old/child", cms.publishedUrl(child));
        Assert.AreEqual("/new/child", cms.publishedUrl(child, cms.listPublicationsByDate(boundary.AddSeconds(1))));
        Assert.AreEqual("", request(0).model<Spages>().resolveRedirect("/old/child"));
        // Advance only these disposable release rows; public resolution still runs through a fresh request.
        DateTime effective = DateTime.UtcNow.AddMinutes(-1);
        fw.db.update(Spages.REVISION_TABLE, new FwDict { ["effective_time"] = effective.AddMinutes(-1) }, new FwDict { ["id"] = fw.db.opIN(new[] { originalParentRevision, childRevision }) });
        fw.db.update(Spages.REVISION_TABLE, new FwDict { ["effective_time"] = effective }, new FwDict { ["id"] = scheduledRevision });
        var anonymous = request(0).model<Spages>();
        Assert.AreEqual("/new/child", anonymous.resolveRedirect("/OLD/CHILD"));
        Assert.AreEqual(child, anonymous.onePublishedByPath("/new/child")["id"].toInt());
        Assert.IsFalse(anonymous.isScheduled(parent));
    }

    [TestMethod]
    public void UrlAndAliasConflictsReservedRoutesAndExternalAliasesAreRejected()
    {
        int page = create("used");
        cms.saveDraft(page, new FwDict { ["url_aliases"] = "/reserved-alias" });
        publish(page);
        int duplicate = create("used");
        Assert.ThrowsExactly<UserException>(() => publish(duplicate));
        int admin = create("Admin");
        Assert.ThrowsExactly<UserException>(() => publish(admin));
        int competingAlias = create("competing");
        cms.saveDraft(competingAlias, new FwDict { ["url_aliases"] = "/RESERVED-ALIAS" });
        Assert.ThrowsExactly<UserException>(() => publish(competingAlias));
        cms.saveDraft(competingAlias, new FwDict { ["url_aliases"] = "/used" });
        Assert.ThrowsExactly<UserException>(() => publish(competingAlias));
        Assert.ThrowsExactly<UserException>(() =>
        {
            cms.saveDraft(competingAlias, new FwDict { ["url_aliases"] = "https://elsewhere.test/" });
            publish(competingAlias);
        });
        Assert.AreEqual(0, request(0).model<Spages>().onePublished(competingAlias).Count);
        Assert.AreEqual("/used", request(0).model<Spages>().resolveRedirect("/reserved-alias"));
    }

    [TestMethod]
    public void StarterPagesArePublishedAndMigrationLeavesConvertedRowsUntouched()
    {
        Assert.AreEqual(1, request(0).model<Spages>().onePublishedByPath("/")["id"].toInt());
        Assert.AreEqual(2, request(0).model<Spages>().onePublishedByPath("/test-page")["id"].toInt());
        int revisions = cms.listRevisions(2).Count;
        Assert.AreEqual(isBaseline ? 2 : 1, revisions);
        Assert.AreEqual(Spages.KIND_PUBLISHED, cms.listRevisions(2)[0]["kind"].toInt());
        Assert.AreEqual(0, migrate());
        Assert.AreEqual(revisions, cms.listRevisions(2).Count);
    }

    [TestMethod]
    public void DemoSqlPreservesHomepageAndExistingDraftsAcrossRepeatedSeeding()
    {
        string home = fw.db.value("spages", new FwDict { ["id"] = 1 }, "draft_json").toStr();
        int existing = create("DEMO-SERVICES");
        save(existing, "Existing authored services draft");
        string existingDraft = fw.db.value("spages", new FwDict { ["id"] = existing }, "draft_json").toStr();
        // Older draft storage may keep the slug only in the snapshot.
        fw.db.update("spages", new FwDict { ["url"] = "" }, new FwDict { ["id"] = existing });
        fw.db.insert("spages", new FwDict
        {
            ["iname"] = "Malformed draft",
            ["url"] = "malformed",
            ["draft_json"] = "{invalid",
            ["status"] = 10
        });
        string sqlPath = Path.Combine(root(), "osafw-app/App_Data/sql", isSqlServer ? "" : "sqlite", "demo.sql");
        string sql = File.ReadAllText(sqlPath);
        int start = sql.IndexOf("/* CMS demonstration drafts */", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0);
        string seeds = sql[start..];
        fw.db.execMultipleSQL(seeds);
        int rowsAfterFirstRun = fw.db.valuep("SELECT COUNT(*) FROM spages").toInt();
        fw.db.execMultipleSQL(seeds);
        Assert.AreEqual(rowsAfterFirstRun, fw.db.valuep("SELECT COUNT(*) FROM spages").toInt());
        Assert.AreEqual(home, fw.db.value("spages", new FwDict { ["id"] = 1 }, "draft_json").toStr());
        Assert.AreEqual(existingDraft, fw.db.value("spages", new FwDict { ["id"] = existing }, "draft_json").toStr());
        int snippet = fw.db.value("spages", new FwDict { ["url"] = "demo-help" }, "id").toInt();
        int department = fw.db.value("spages", new FwDict { ["url"] = "demo-department" }, "id").toInt();
        Assert.IsTrue(snippet > 0 && department > 0);
        Assert.AreEqual("article", cms.oneDraftOrFail(snippet)["template"]);
        Assert.AreEqual("sidebar-right", cms.oneDraftOrFail(department)["template"]);
        Assert.AreEqual(0, cms.onePublished(department, 0).Count);
        Assert.ThrowsExactly<UserException>(() => publish(department));
        publish(snippet);
        publish(department);
        StringAssert.Contains(cms.publishedText(cms.onePublished(department)), "Tell us what you need.");
    }

    [TestMethod]
    public void LaterScheduledCollisionAndSnippetAncestorWithdrawalAreRejected()
    {
        int first = create("first");
        publish(first);
        cms.saveDraft(first, new FwDict { ["url"] = "reserved-later" });
        publish(first, DateTime.UtcNow.AddDays(2));
        int second = create("reserved-later");
        Assert.ThrowsExactly<UserException>(() => publish(second, DateTime.UtcNow.AddDays(1)));
        int parent = create("department");
        publish(parent);
        int snippet = cms.saveDraft(0, new FwDict
        {
            ["iname"] = "Department help",
            ["is_snippet"] = 1,
            ["url"] = "department-help",
            ["parent_id"] = parent,
            ["template"] = "article",
            ["content_json"] = content("Help")
        });
        publish(snippet);
        int consumer = create("consumer", snippet: "department-help");
        publish(consumer);
        Assert.ThrowsExactly<UserException>(() => cms.updateWorkflow(parent, "unpublish"));
    }

    [TestMethod]
    public void MigrationPreservesOriginalColumnsCustomizationsAndFuturePublication()
    {
        DateTime date = DateTime.UtcNow.AddDays(3);
        const string ORIGINAL = "## Introduction\n\nUseful **text**.\n\n- A\n- B\n\n<div>Unsupported markup</div>";
        int id = fw.db.insert("spages", new FwDict
        {
            ["iname"] = "Migration",
            ["url"] = "migration",
            ["idesc"] = ORIGINAL,
            ["idesc_left"] = "Left content",
            ["idesc_right"] = "Right content",
            ["template"] = "application-original",
            ["custom_head"] = "<meta name=\"migration\" content=\"kept\">",
            ["custom_css"] = ".demo { color: red; }",
            ["custom_js"] = "window.migration = true;",
            ["pub_time"] = date,
            ["status"] = 0
        });
        Assert.AreEqual(0, request(0).model<Spages>().onePublished(id).Count);
        Assert.AreEqual(1, migrate());
        var draft = cms.oneDraftOrFail(id);
        Assert.AreEqual("three-column", draft["template"]);
        Assert.AreEqual(".demo { color: red; }", draft["custom_css"]);
        Assert.AreEqual("window.migration = true;", draft["custom_js"]);
        Assert.AreEqual(ORIGINAL, fw.db.value("spages", new FwDict { ["id"] = id }, "idesc"));
        Assert.AreEqual(0, request(0).model<Spages>().onePublished(id).Count);
        Assert.IsTrue(cms.listPublicationsByDate(date.AddSeconds(1)).ContainsKey(id));
        Assert.AreEqual(0, migrate());
        var doc = SpagesContent.parse(draft["content_json"].toStr());
        StringAssert.Contains(SpagesContent.renderRegion(doc, "left", new()), "Left content");
        StringAssert.Contains(SpagesContent.renderRegion(doc, "right", new()), "Right content");
        var original = cms.oneRevisionOrFail(id, cms.listRevisions(id).Last()["id"].toInt());
        Assert.AreEqual(Spages.KIND_ORIGINAL, cms.listRevisions(id).Last()["kind"].toInt());
        Assert.AreEqual(ORIGINAL, original["idesc"]);
        Assert.AreEqual("application-original", original["original_template"]);
        StringAssert.Contains(original["content_json"].toStr(), "Introduction");
        var originalPreview = (FwDict)cms.buildPageState(original, true, true)["page"]!;
        StringAssert.Contains(originalPreview["html_main"].toStr(), "Introduction");
    }

    [TestMethod]
    public void UnconvertedRowsStayPrivateAndMigrationRetainsInactiveAndDeletedOriginals()
    {
        int id = fw.db.insert("spages", new FwDict
        {
            ["iname"] = "Before conversion",
            ["url"] = "before-conversion",
            ["idesc"] = "Original source",
            ["status"] = 0
        });
        Assert.ThrowsExactly<UserException>(() => cms.saveDraft(id, new FwDict { ["iname"] = "New draft" }, true));
        foreach (string action in new[]
        {
            "submit",
            "publish",
            "cancel",
            "changes",
            "unpublish"
        }

        )
        {
            Assert.ThrowsExactly<UserException>(() => cms.updateWorkflow(id, action, "Review note"));
        }

        Assert.AreEqual("", fw.db.value("spages", new FwDict { ["id"] = id }, "draft_json").toStr());
        Assert.AreEqual(0, request(0).model<Spages>().oneByFullUrl("/before-conversion").Count);
        Assert.AreEqual(0, cms.listRevisions(id).Count);
        int inactive = fw.db.insert("spages", new FwDict
        {
            ["iname"] = "Inactive original",
            ["url"] = "inactive-original",
            ["idesc"] = "Unpublished source",
            ["status"] = 10
        });
        int deleted = fw.db.insert("spages", new FwDict
        {
            ["iname"] = "Deleted original",
            ["url"] = "deleted-original",
            ["idesc"] = "Recoverable source",
            ["status"] = 127
        });
        Assert.AreEqual(3, migrate());
        cms.saveDraft(id, new FwDict { ["iname"] = "New draft" });
        Assert.AreEqual("Original source", cms.oneRevisionOrFail(id, cms.listRevisions(id).Last()["id"].toInt())["idesc"]);
        foreach (int unpublished in new[]
        {
            inactive,
            deleted
        }

        )
        {
            var revisions = cms.listRevisions(unpublished);
            Assert.AreEqual(2, revisions.Count);
            Assert.AreEqual(Spages.KIND_SAVED, revisions[0]["kind"].toInt());
            Assert.AreEqual(Spages.KIND_ORIGINAL, revisions[1]["kind"].toInt());
            Assert.AreEqual(0, request(0).model<Spages>().onePublished(unpublished).Count);
        }

        Assert.AreEqual("Recoverable source", cms.oneRevisionOrFail(deleted, cms.listRevisions(deleted).Last()["id"].toInt())["idesc"]);
        Assert.AreEqual(0, migrate());
    }

    [TestMethod]
    public void MigrationRequiresSiteAdminPostAndCurrentXssToken()
    {
        int id = fw.db.insert("spages", new FwDict
        {
            ["iname"] = "Protected migration",
            ["url"] = "protected-migration",
            ["idesc"] = "Keep original",
            ["status"] = 0
        });
        var get = migrationController(request(), "GET");
        Assert.ThrowsExactly<AuthException>(() => get.MigrateAction());
        var missingToken = migrationController(request(), isTokenIncluded: false);
        Assert.ThrowsExactly<AuthException>(() => missingToken.MigrateAction());
        var editor = migrationController(request(80));
        Assert.ThrowsExactly<AuthException>(() => editor.MigrateAction());
        Assert.AreEqual("", fw.db.value("spages", new FwDict { ["id"] = id }, "draft_json").toStr());
        Assert.AreEqual(0, cms.listRevisions(id).Count);
        Assert.AreEqual(1, migrate());
        Assert.AreEqual("Keep original", cms.oneRevisionOrFail(id, cms.listRevisions(id).Last()["id"].toInt())["idesc"]);
    }

    [TestMethod]
    public void FailedMigrationRollsBackItsSnapshotAndCanResume()
    {
        int id = fw.db.insert("spages", new FwDict
        {
            ["iname"] = "Retry migration",
            ["url"] = "retry-migration",
            ["idesc"] = "Keep recoverable source",
            ["status"] = 0
        });
        string trigger = isSqlServer ? "CREATE TRIGGER spages_cms_migration_failure ON spages_revisions AFTER INSERT AS BEGIN " + "IF EXISTS (SELECT 1 FROM inserted WHERE spages_id=" + id + " AND kind=30) " + "BEGIN; THROW 50000, 'Simulated migration failure', 1; END; END;" : "CREATE TRIGGER spages_cms_migration_failure BEFORE INSERT ON spages_revisions " + "WHEN NEW.spages_id=" + id + " AND NEW.kind=30 " + "BEGIN SELECT RAISE(ABORT, 'Simulated migration failure'); END;";
        fw.db.exec(trigger);
        try
        {
            if (isSqlServer)
            {
                Assert.ThrowsExactly<Microsoft.Data.SqlClient.SqlException>(() => migrate());
            }
            else
                Assert.ThrowsExactly<Microsoft.Data.Sqlite.SqliteException>(() => migrate());
            Assert.AreEqual("", fw.db.value("spages", new FwDict { ["id"] = id }, "draft_json").toStr());
            Assert.AreEqual("Keep recoverable source", fw.db.value("spages", new FwDict { ["id"] = id }, "idesc"));
            Assert.AreEqual(0, cms.listRevisions(id).Count, "The original snapshot must roll back with the failed publication.");
            Assert.AreEqual(0, request(0).model<Spages>().onePublished(id).Count);
        }
        finally
        {
            fw.db.exec("DROP TRIGGER spages_cms_migration_failure");
        }

        Assert.AreEqual(1, migrate());
        Assert.AreEqual(2, cms.listRevisions(id).Count);
        Assert.AreEqual(id, request(0).model<Spages>().onePublishedByPath("/retry-migration")["id"].toInt());
        Assert.AreEqual(0, migrate());
    }

    [TestMethod]
    public void ManualAliasesPublishAndDisappearWithTheirApprovedSnapshot()
    {
        int page = create("canonical");
        publish(page);
        cms.saveDraft(page, new FwDict { ["url_aliases"] = "/old-name\n/another-name" });
        Assert.AreEqual("", request(0).model<Spages>().resolveRedirect("/old-name"));
        publish(page);
        Assert.AreEqual("/canonical", request(0).model<Spages>().resolveRedirect("/OLD-NAME"));
        cms.saveDraft(page, new FwDict { ["url_aliases"] = "/another-name" });
        Assert.AreEqual("/canonical", request(0).model<Spages>().resolveRedirect("/old-name"));
        publish(page);
        Assert.AreEqual("", request(0).model<Spages>().resolveRedirect("/old-name"));
        Assert.AreEqual("/canonical", request(0).model<Spages>().resolveRedirect("/another-name"));
    }

    [TestMethod]
    public void PageRedirectLoopsThroughCaseInsensitiveAliasesAreRejected()
    {
        int first = create("first");
        cms.saveDraft(first, new FwDict { ["redirect_url"] = "/second", ["url_aliases"] = "/old-name" });
        publish(first);
        int second = create("second");
        cms.saveDraft(second, new FwDict { ["redirect_url"] = "/OLD-NAME" });
        Assert.ThrowsExactly<UserException>(() => publish(second));
        Assert.AreEqual(0, request(0).model<Spages>().onePublished(second).Count);
    }

}
#endif
