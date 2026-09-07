#if isSQLite
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
    public void RagSearchRejectsIndexedPageTextAfterSnippetOrPagePublicationChanges()
    {
        enableRagQueue();
        int snippet = cms.saveDraft(0, new FwDict
        {
            ["iname"] = "RAG shared text",
            ["is_snippet"] = 1,
            ["url"] = "rag-shared",
            ["template"] = "article",
            ["content_json"] = content("Original shared retrieval text")
        });
        publish(snippet);
        int dependent = create("rag-dependent", snippet: "rag-shared");
        publish(dependent);
        int stable = create("rag-stable");
        save(stable, "Stable retrieval text");
        publish(stable);
        addIndexedSpageChunk(dependent, "Original shared retrieval text", [1.0f, 0.0f]);
        addIndexedSpageChunk(stable, "Stable retrieval text", [0.0f, 1.0f]);
        var initial = ragResultPageIds(request(0));
        CollectionAssert.Contains(initial, dependent, "A current indexed page must remain eligible.");
        CollectionAssert.Contains(initial, stable, "The preserved current source must remain eligible.");
        save(snippet, "Revised shared retrieval text");
        publish(snippet);
        var afterSnippetRevision = ragResultPageIds(request(0));
        CollectionAssert.DoesNotContain(afterSnippetRevision, dependent, "A referenced snippet change must invalidate the page's old indexed text.");
        CollectionAssert.Contains(afterSnippetRevision, stable);
        Assert.IsTrue(fw.model<RagSources>().queueSpage(dependent));
        int dependentSource = spageSourceId(dependent);
        fw.model<RagChunks>().deleteBySource(dependentSource);
        addIndexedSpageChunk(dependent, "Revised shared retrieval text", [1.0f, 0.0f]);
        CollectionAssert.Contains(ragResultPageIds(request(0)), dependent, "A freshly indexed current source must become eligible again.");
        save(dependent, "Replacement page retrieval text");
        publish(dependent);
        var afterPageRevision = ragResultPageIds(request(0));
        CollectionAssert.DoesNotContain(afterPageRevision, dependent, "A page publication change must invalidate its old indexed text.");
        CollectionAssert.Contains(afterPageRevision, stable);
    }

    [TestMethod]
    public void SearchAndXmlSitemapExposeOnlyCurrentAnonymousPublications()
    {
        const string MARKER = "public-boundary-marker-6f9c";
        int visible = create("boundary-visible");
        save(visible, MARKER + " visible");
        publish(visible);
        int draft = create("boundary-draft");
        save(draft, MARKER + " draft");
        int future = create("boundary-future");
        save(future, MARKER + " future");
        publish(future, DateTime.UtcNow.AddHours(1));
        int restrictedParent = create("boundary-internal", access: 50);
        publish(restrictedParent);
        int restrictedChild = create("boundary-child", restrictedParent);
        save(restrictedChild, MARKER + " restricted child");
        publish(restrictedChild);
        int noindexPage = create("boundary-noindex");
        cms.saveDraft(noindexPage, new FwDict { ["content_json"] = content(MARKER + " noindex"), ["is_noindex"] = 1 });
        publish(noindexPage);
        var searchFw = request(0);
        searchFw.FORM["s"] = MARKER;
        searchFw.setController("Search", "Index");
        var search = new SearchController();
        search.init(searchFw);
        search.checkAccess();
        var searchState = search.IndexAction();
        var searchIds = ((FwList)searchState["results"]!).Select(x => ((FwDict)x)["id"].toInt()).ToArray();
        CollectionAssert.AreEquivalent(new[] { visible }, searchIds);
        var parser = new ParsePage(new ParsePageOptions
        {
            TemplatesRoot = Path.Combine(root(), "osafw-app/App_Data/template"),
            IsLangUpdate = false,
            GlobalsGetter = () => new FwDict { ["ROOT_URL"] = "/portal" }
        });
        StringAssert.Contains(parser.parse_page("/search/index", "main.html", searchState), "href=\"/portal/boundary-visible\"");
        StringAssert.Contains(parser.parse_page("/sitemap/index", "main.html", new FwDict { ["cms"] = true, ["pages"] = searchFw.model<Spages>().listPublished() }), "href=\"/portal/boundary-visible\"");
        Assert.AreEqual("private, no-store", searchFw.response.Headers.CacheControl.ToString());
        Assert.AreEqual("noindex", searchFw.response.Headers["X-Robots-Tag"].ToString());
        var sitemapFw = request(0);
        sitemapFw.response.Body = new MemoryStream();
        sitemapFw.G["PAGE_LAYOUT_PUBLIC"] = "";
        sitemapFw.setController("Sitemap", "Xml");
        var sitemap = new SitemapController();
        sitemap.init(sitemapFw);
        sitemap.checkAccess();
        sitemap.XmlAction();
        sitemapFw.response.Body.Position = 0;
        string xml = new StreamReader(sitemapFw.response.Body).ReadToEnd();
        StringAssert.Contains(xml, "https://example.org/boundary-visible");
        Assert.IsFalse(xml.Contains("boundary-draft", StringComparison.Ordinal));
        Assert.IsFalse(xml.Contains("boundary-future", StringComparison.Ordinal));
        Assert.IsFalse(xml.Contains("boundary-child", StringComparison.Ordinal));
        Assert.IsFalse(xml.Contains("boundary-noindex", StringComparison.Ordinal));
        Assert.AreEqual("application/xml; charset=utf-8", sitemapFw.response.ContentType);
    }

    [TestMethod]
    public void PreviewRequiresCmsAccessAndMarksDraftResponsePrivateAndNoindex()
    {
        int page = create("preview-private");
        save(page, "Unpublished preview marker 54b7");
        var deniedFw = request(79);
        deniedFw.setController("AdminSpages", "Preview");
        var denied = new AdminSpagesController();
        denied.init(deniedFw);
        Assert.ThrowsExactly<AuthException>(() => denied.checkAccess());
        Assert.ThrowsExactly<AuthException>(() => denied.PreviewAction(page));
        var adminFw = request(100);
        adminFw.response.Body = new MemoryStream();
        adminFw.setController("AdminSpages", "Preview");
        var preview = new AdminSpagesController();
        preview.init(adminFw);
        preview.checkAccess();
        preview.PreviewAction(page);
        Assert.AreEqual("private, no-store", adminFw.response.Headers.CacheControl.ToString());
        Assert.AreEqual("noindex, nofollow", adminFw.response.Headers["X-Robots-Tag"].ToString());
        Assert.AreEqual(0, request(0).model<Spages>().onePublished(page).Count, "Preview must not publish the draft.");
    }

#if isRoles
    [TestMethod]
    public void DraftAttachmentRequiresPagePreviewPermissionInAdditionToAttView()
    {
        int editorId = fw.db.insert("users", new FwDict
        {
            ["fname"] = "CMS",
            ["lname"] = "Editor",
            ["email"] = "cms-editor-" + Guid.NewGuid().ToString("N") + "@example.test",
            ["pwd"] = "test-only",
            ["access_level"] = 80,
            ["status"] = FwModel.STATUS_ACTIVE
        });
        int editorRoleId = fw.db.value("roles", new FwDict { ["iname"] = "Employee" }, "id").toInt();
        fw.db.exec("INSERT INTO users_roles (users_id,roles_id,status) VALUES (@user_id,@role_id,@status)", new FwDict
        {
            ["user_id"] = editorId,
            ["role_id"] = editorRoleId,
            ["status"] = FwModel.STATUS_ACTIVE
        });
        grantRoleView(editorRoleId, "Att");
        int pageEntityId = fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_SPAGE);
        int draftPage = create("role-draft-attachment");
        int draftAttachment = addPageAttachment(pageEntityId, draftPage, "draft-owned.txt");
        cms.saveDraft(draftPage, new FwDict { ["content_json"] = contentWithFile(draftAttachment, "Draft file") });
        int publicPage = create("role-public-attachment");
        int publicAttachment = addPageAttachment(pageEntityId, publicPage, "public-owned.txt");
        cms.saveDraft(publicPage, new FwDict { ["content_json"] = contentWithFile(publicAttachment, "Public file") });
        publish(publicPage);
        var deniedFw = request(80);
        deniedFw.Session("user_id", editorId.ToString());
        deniedFw.setController("Att", "Show");
        var deniedEntry = new AttController();
        deniedEntry.init(deniedFw);
        deniedEntry.checkAccess();
        Assert.IsTrue(deniedFw.model<Users>().isAccessByRolesResourcePermission(editorId, "Att", Permissions.PERMISSION_VIEW));
        Assert.IsFalse(deniedFw.model<Users>().isAccessByRolesResourcePermission(editorId, "AdminSpages", Permissions.PERMISSION_VIEW));
        Assert.ThrowsExactly<AuthException>(() => deniedFw.model<Att>().checkAccess(draftAttachment));
        request(0).model<Att>().checkAccess(publicAttachment);
        grantRoleView(editorRoleId, "AdminSpages");
        var allowedFw = request(80);
        allowedFw.Session("user_id", editorId.ToString());
        allowedFw.setController("Att", "Show");
        var allowedEntry = new AttController();
        allowedEntry.init(allowedFw);
        allowedEntry.checkAccess();
        Assert.IsTrue(allowedFw.model<Users>().isAccessByRolesResourcePermission(editorId, "AdminSpages", Permissions.PERMISSION_VIEW));
        allowedFw.model<Att>().checkAccess(draftAttachment);
    }

#endif
    private void enableRagQueue()
    {
        bool isLoggingEvents = fw.is_log_events;
        fw.is_log_events = false;
        try
        {
            var settings = fw.model<Settings>();
            settings.setValue("ASSISTANT_ENABLED", "1");
            settings.setValue("OPENAI_API_KEY", "test-only-not-a-secret");
        }
        finally
        {
            fw.is_log_events = isLoggingEvents;
        }
    }

#if isRoles
    private void loadRolesSchema()
    {
        string rolesPath = Path.Combine(root(), "osafw-app", "App_Data", "sql", isSqlServer ? "roles.sql" : Path.Combine("sqlite", "roles.sql"));
        fw.db.execMultipleSQL(File.ReadAllText(rolesPath));
    }

    private void grantRoleView(int roleId, string resourceCode)
    {
        int resourceId = fw.db.value("resources", new FwDict { ["icode"] = resourceCode }, "id").toInt();
        int permissionId = fw.db.value("permissions", new FwDict { ["icode"] = Permissions.PERMISSION_VIEW }, "id").toInt();
        Assert.IsTrue(resourceId > 0 && permissionId > 0, "The isolated roles schema must seed the requested resource and view permission.");
        fw.db.exec("INSERT INTO roles_resources_permissions (roles_id,resources_id,permissions_id,status) VALUES (@role_id,@resource_id,@permission_id,@status)", new FwDict
        {
            ["role_id"] = roleId,
            ["resource_id"] = resourceId,
            ["permission_id"] = permissionId,
            ["status"] = FwModel.STATUS_ACTIVE
        });
    }

    private int addPageAttachment(int pageEntityId, int pageId, string filename)
    {
        return fw.db.insert("att", new FwDict
        {
            ["icode"] = Guid.NewGuid().ToString("N"),
            ["fwentities_id"] = pageEntityId,
            ["item_id"] = pageId,
            ["iname"] = filename,
            ["fname"] = filename,
            ["fsize"] = 1,
            ["ext"] = ".txt",
            ["status"] = FwModel.STATUS_ACTIVE
        });
    }

    private static string contentWithFile(int attachmentId, string title)
    {
        var document = SpagesContent.empty();
        var blocks = (JsonArray)document["regions"]!["main"]!["blocks"]!;
        blocks.Add(new JsonObject { ["type"] = "file", ["data"] = new JsonObject { ["att_id"] = attachmentId, ["title"] = title } });
        return document.ToJsonString();
    }

#endif
    private int spageSourceId(int pageId)
    {
        int entityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_SPAGE);
        var source = fw.model<RagSources>().oneBySourceKey(RagSources.BuildSourceKey(RagSources.SOURCE_TYPE_SPAGE, entityId, pageId, 0));
        Assert.IsTrue(source.Count > 0, "Publishing with the RAG queue enabled must create the page source.");
        return source["id"].toInt();
    }

    private void addIndexedSpageChunk(int pageId, string text, List<float> embedding)
    {
        int sourceId = spageSourceId(pageId);
        var page = cms.onePublished(pageId, 100);
        fw.model<RagChunks>().addEmbedding(new RagChunks.ChunkEmbedding
        {
            RagSourcesId = sourceId,
            FwEntitiesId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_SPAGE),
            ItemId = pageId,
            SourceType = RagSources.SOURCE_TYPE_SPAGE,
            SourceTitle = page["iname"].toStr(),
            SourceUrl = cms.publishedUrl(pageId),
            Filename = page["iname"].toStr(),
            Text = text,
            Embedding = embedding,
            EmbeddingModel = LLM.MODEL_TEXT_EMBEDDING_3_SMALL
        });
        fw.model<RagSources>().markIndexed(sourceId);
    }

    private static int[] ragResultPageIds(FW current)
    {
        return current.model<RagChunks>().listByJsonQuery("[1.0,0.0]", 1.0, 2, LLM.MODEL_TEXT_EMBEDDING_3_SMALL, 20).Select(x => x["item_id"].toInt()).Distinct().ToArray();
    }

}
#endif
