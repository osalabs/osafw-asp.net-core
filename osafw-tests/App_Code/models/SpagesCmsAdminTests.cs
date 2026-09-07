#if isSQLite
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

namespace osafw.Tests;

public partial class SpagesCmsTests
{
    [TestMethod]
    public void WorkingMetadataUpgradeMakesExistingDraftsSearchableWithoutChangingHistory()
    {
        int page = create("metadata-upgrade");
        int image = fw.db.insert("att", new FwDict
        {
            ["iname"] = "Metadata image", ["fname"] = "metadata.png",
            ["ext"] = "png", ["is_image"] = 1, ["status"] = FwModel.STATUS_ACTIVE
        });
        cms.saveDraft(page, new FwDict
        {
            ["meta_description"] = "Working description marker",
            ["redirect_url"] = "/Contact", ["head_att_id"] = image
        });
        string draft = fw.db.value("spages", DB.h("id", page), "draft_json").toStr();
        int revisions = cms.listRevisions(page).Count;
        fw.db.update("spages", new FwDict
        {
            ["meta_description"] = "Previous description", ["redirect_url"] = "", ["head_att_id"] = null
        }, DB.h("id", page));
        string script = File.ReadAllText(Path.Combine(root(), "osafw-app/App_Data/sql",
            isSqlServer ? "" : "sqlite", "updates/upd2026-09-06-spages-working-metadata.sql"));
        fw.db.execMultipleSQL(script);
        fw.db.execMultipleSQL(script);
        var row = fw.db.row("spages", DB.h("id", page));
        Assert.AreEqual("Working description marker", row["meta_description"].toStr());
        Assert.AreEqual("/Contact", row["redirect_url"].toStr());
        Assert.AreEqual(image, row["head_att_id"].toInt());
        Assert.AreEqual(draft, row["draft_json"].toStr());
        Assert.AreEqual(revisions, cms.listRevisions(page).Count);
        var current = request();
        current.FORM["f"] = new FwDict { ["s"] = "Working description marker", ["status"] = Spages.STATUS_DRAFT };
        Assert.AreEqual(1, adminController(current).IndexAction()["count"].toInt());

        foreach (string invalid in new[] { "{}", "not json", "{\"head_att_id\":\"invalid\"}", "{\"head_att_id\":2147483647}" })
        {
            fw.db.update("spages", DB.h("draft_json", invalid), DB.h("id", page));
            fw.db.execMultipleSQL(script);
            Assert.AreEqual(image, fw.db.value("spages", DB.h("id", page), "head_att_id").toInt());
            Assert.AreEqual("Working description marker", fw.db.value("spages", DB.h("id", page), "meta_description").toStr());
        }
        foreach (string emptyImage in new[] { "{\"head_att_id\":null}", "{\"head_att_id\":0}" })
        {
            fw.db.update("spages", new FwDict { ["draft_json"] = emptyImage, ["head_att_id"] = image }, DB.h("id", page));
            fw.db.execMultipleSQL(script);
            Assert.AreEqual(0, fw.db.value("spages", DB.h("id", page), "head_att_id").toInt());
        }
    }

    private AdminSpagesController adminController(FW current, string action = "Index", string method = "GET")
    {
        current.setController("AdminSpages", action);
        current.route.method = method;
        var controller = new AdminSpagesController();
        controller.init(current);
        controller.checkAccess();
        return controller;
    }

    [TestMethod]
    public void StandardListPagesAndFiltersWorkingDraftMetadataInSql()
    {
        int first = create("list-first");
        publish(first);
        cms.saveDraft(first, new FwDict { ["iname"] = "List marker A", ["url"] = "draft-first" });
        int second = create("list-second");
        cms.saveDraft(second, new FwDict { ["iname"] = "List marker B" });
        var current = request();
        current.FORM["f"] = new FwDict
        {
            ["s"] = "List marker", ["status"] = Spages.STATUS_DRAFT,
            ["is_snippet"] = 0, ["pagesize"] = 1, ["pagenum"] = 1,
            ["sortby"] = "iname", ["sortdir"] = "asc"
        };
        var state = adminController(current).IndexAction();
        Assert.AreEqual(2, state["count"].toInt());
        var rows = (FwList)state["list_rows"]!;
        Assert.HasCount(1, rows);
        Assert.AreEqual(second, rows[0]["id"].toInt());
        Assert.IsTrue(state.ContainsKey("list_headers"));
        Assert.IsTrue(state.ContainsKey("pager"));
        Assert.IsFalse(rows[0].ContainsKey("draft_json"), "List rows must not materialize content documents.");
        Assert.AreEqual("/list-first", request(0).model<Spages>().getFullUrl(first));
        Assert.AreEqual("draft-first", fw.db.value("spages", DB.h("id", first), "url").toStr());
    }

    [TestMethod]
    public void StandardPublishedFilterResolvesElapsedScheduleWithoutUpdatingTheRow()
    {
        int page = create("elapsed-status");
        int revision = publish(page, DateTime.UtcNow.AddHours(1));
        fw.db.update("spages_revisions", DB.h("effective_time", DateTime.UtcNow.AddHours(-1)), DB.h("id", revision));
        var current = request();
        current.FORM["f"] = new FwDict { ["s"] = "elapsed-status", ["status"] = 0 };
        var state = adminController(current).IndexAction();
        Assert.AreEqual(1, state["count"].toInt());
        Assert.AreEqual(Spages.STATUS_PUBLISHED, ((FwList)state["list_rows"]!)[0]["status"].toInt());
        Assert.AreEqual(Spages.STATUS_SCHEDULED, fw.db.value("spages", DB.h("id", page), "status").toInt());
    }

    [TestMethod]
    public void SinglePublicationLookupFiltersInSqlAndReusesItsResolvedUrl()
    {
        int page = create("query-target");
        publish(page);
        var current = request(0);
        current.db.valuep("SELECT 1");
        var model = current.model<Spages>();
        int before = DB.SQL_QUERY_CTR;
        Assert.AreEqual(page, model.oneByUrl("query-target", 0)["id"].toInt());
        Assert.AreEqual(1, DB.SQL_QUERY_CTR - before, "A root lookup should execute one filtered revision query.");
        StringAssert.Contains(DB.last_sql, "@url");
        StringAssert.Contains(DB.last_sql, "@parent_id");
        before = DB.SQL_QUERY_CTR;
        Assert.AreEqual("/query-target", model.getFullUrl(page));
        Assert.AreEqual(0, DB.SQL_QUERY_CTR - before, "Computing the URL must reuse the resolved publication.");
        Assert.AreEqual(0, request(0).model<Spages>().oneByUrl("query-target", page).Count);
    }

    [TestMethod]
    public void TrashAndRestoreRetainHistoryWithoutRepublishing()
    {
        int page = create("trash-state");
        publish(page);
        cms.delete(page);
        Assert.AreEqual(Spages.STATUS_DELETED, cms.oneDraftOrFail(page)["status"].toInt());
        Assert.AreEqual(0, request(0).model<Spages>().onePublished(page).Count);
        cms.updateRestoreDeleted(page);
        Assert.AreEqual(Spages.STATUS_DRAFT, cms.oneDraftOrFail(page)["status"].toInt());
        Assert.AreEqual(0, request(0).model<Spages>().onePublished(page).Count);
        Assert.IsTrue(cms.listRevisions(page).Any(row => row["kind"].toInt() == Spages.KIND_PUBLISHED));
        publish(page);
        Assert.IsTrue(request(0).model<Spages>().onePublished(page).Count > 0);
        int home = fw.db.value("spages", DB.h("is_home", 1), "id").toInt();
        Assert.ThrowsExactly<UserException>(() => cms.updateWorkflow(home, "delete"));
    }

    [TestMethod]
    public void BulkActionsRequireTokenAndValidateAllIdsBeforeChangingRows()
    {
        int page = create("bulk-state");
        var current = request();
        current.FORM["cb"] = new FwDict { [page.ToString()] = 1 };
        current.FORM["cms_action"] = "submit";
        var controller = adminController(current, "SaveMulti", "PUT");
        current.Session("XSS", "bulk-token");
        Assert.ThrowsExactly<AuthException>(() => controller.SaveMultiAction());
        current.FORM["XSS"] = "bulk-token";
        current.FORM["cb"] = new FwDict { [page.ToString()] = 1, ["99999999"] = 1 };
        Assert.ThrowsExactly<NotFoundException>(() => controller.SaveMultiAction());
        Assert.AreEqual(Spages.STATUS_DRAFT, cms.oneDraftOrFail(page)["status"].toInt());
        current.FORM["cb"] = new FwDict { [page.ToString()] = 1 };
        current.request.Headers.Accept = "application/json";
        controller.SaveMultiAction();
        Assert.AreEqual(Spages.STATUS_IN_REVIEW, cms.oneDraftOrFail(page)["status"].toInt());
        Assert.ThrowsExactly<AuthException>(() => adminController(request(79), "SaveMulti", "PUT"));
    }

    [TestMethod]
    public void LegacyTreeHelpersRenderEscapedSelectedOptionsThroughParsePage()
    {
        var rows = new FwList
        {
            new FwDict { ["id"] = 901, ["parent_id"] = 0, ["iname"] = "<script>alert(1)</script>", ["url"] = "parent" },
            new FwDict { ["id"] = 902, ["parent_id"] = 901, ["iname"] = "Child", ["url"] = "child" }
        };
#pragma warning disable CS0618
        var tree = cms.getPagesTree(rows, 0);
        Assert.HasCount(2, cms.getPagesTreeList(tree));
        string html = cms.getPagesTreeSelectHtml("902", tree);
#pragma warning restore CS0618
        StringAssert.Contains(html, "&lt;script&gt;");
        Assert.IsFalse(html.Contains("<script>"));
        StringAssert.Contains(html, "selected");
    }
    [TestMethod]
    public void TitleSortOrdersSiblingsWithinParentsBeforePagingAndOtherSortsStayFlat()
    {
        int parent = create("tree-parent");
        int child = create("tree-child", parent);
        int other = create("tree-other");
        int sibling = create("tree-sibling", parent);
        cms.saveDraft(parent, DB.h("iname", "Tree Z parent"));
        cms.saveDraft(child, DB.h("iname", "Tree A child"));
        cms.saveDraft(other, DB.h("iname", "Tree M other"));
        cms.saveDraft(sibling, DB.h("iname", "Tree B sibling"));

        FwList list(string sort, string direction, int page = 0, int size = 25, string search = "Tree ")
        {
            var current = request();
            current.FORM["f"] = new FwDict
            {
                ["s"] = search, ["sortby"] = sort, ["sortdir"] = direction,
                ["pagesize"] = size, ["pagenum"] = page
            };
            return (FwList)adminController(current).IndexAction()["list_rows"]!;
        }

        var rows = list("iname", "asc");
        CollectionAssert.AreEqual(new[] { other, parent, child, sibling }, rows.Select(row => row["id"].toInt()).ToArray());
        Assert.AreEqual("1.25", rows[2]["tree_indent"].toStr());
        Assert.IsTrue(rows[2]["is_subpage"].toBool());
        CollectionAssert.AreEqual(new[] { parent, sibling, child, other }, list("iname", "desc").Select(row => row["id"].toInt()).ToArray());
        Assert.AreEqual(child, list("iname", "asc", 1, 2)[0]["id"].toInt());
        Assert.AreEqual(child, list("iname", "asc", search: "Tree A")[0]["id"].toInt(), "Filtering out a parent must retain the matching child.");
        var flat = list("id", "asc");
        CollectionAssert.AreEqual(new[] { parent, child, other, sibling }, flat.Select(row => row["id"].toInt()).ToArray());
        Assert.IsFalse(flat.Any(row => row["is_tree"].toBool()));
        Assert.IsFalse(rows.Any(row => row.ContainsKey("draft_json") || row.ContainsKey("content_json")));

        int adjacent(int id, string sort, bool isPrevious = false, string search = "Tree ")
        {
            var current = request();
            current.FORM["f"] = new FwDict { ["s"] = search, ["sortby"] = sort, ["sortdir"] = "asc" };
            current.FORM["edit"] = "1";
            current.FORM["prev"] = isPrevious ? "1" : "0";
            return adminController(current, "Next").NextAction(id.ToString())["id"].toInt();
        }
        Assert.AreEqual(child, adjacent(parent, "iname"));
        Assert.AreEqual(parent, adjacent(child, "iname", true));
        Assert.AreEqual(other, adjacent(child, "id"));
        Assert.AreEqual(child, adjacent(parent, "iname", search: "Tree A"));
    }

    [TestMethod]
    public void AdminHierarchyKeepsAssociatedSnippetsAndMalformedCyclesVisibleOnce()
    {
        var rows = new DBList
        {
            new DBRow { ["id"] = "3", ["parent_id"] = "1", ["is_snippet"] = "1" },
            new DBRow { ["id"] = "1", ["parent_id"] = "2" },
            new DBRow { ["id"] = "2", ["parent_id"] = "1" },
            new DBRow { ["id"] = "4", ["parent_id"] = "999" }
        };
        var result = cms.listAdminTreeRows(rows);
        Assert.HasCount(4, result);
        Assert.AreEqual(4, result.Select(row => row["id"]).Distinct().Count());
        Assert.AreEqual("3", result[0]["id"]);
        Assert.AreEqual("0", result[0]["is_subpage"]);
        Assert.AreEqual("0", result[0]["tree_indent"]);
    }

    [TestMethod]
    public void ParentOptionsIncludeDraftPathsWithoutDuplicatingHomeSlash()
    {
        int home = fw.db.value("spages", DB.h("is_home", 1), "id").toInt();
        int parent = create("url-parent", home);
        int child = create("url-child", parent);
        var options = cms.listSelectOptionsParents(0);
        Assert.AreEqual("/url-parent/url-child", options.Single(row => row["id"].toInt() == child)["full_url"].toStr());
        Assert.IsFalse(cms.listSelectOptionsParents(parent).Any(row => row["id"].toInt() == child));
        var state = adminController(request(), "ShowForm").ShowFormAction(child);
        Assert.AreEqual("https://example.org/url-parent/", state["parent_url_prefix"].toStr());
    }

    [TestMethod]
    public void StandardImagePickerLimitsImagesToPageAndPublicLibraryAndAuthorAccess()
    {
        int page = create("picker-page", access: 80);
        int other = create("picker-other", access: 80);
        int entity = fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_SPAGE);
        int add(int owner, bool isImage = true, int status = 0) => fw.db.insert("att", new FwDict
        {
            ["iname"] = "Picker image", ["fname"] = "picker.png", ["icode"] = Guid.NewGuid().ToString("N"),
            ["is_image"] = isImage ? 1 : 0, ["status"] = status,
            ["fwentities_id"] = owner > 0 ? entity : null, ["item_id"] = owner > 0 ? owner : null
        });
        int bannerCategory = fw.db.insert("att_categories", new FwDict
        {
            ["icode"] = AttCategories.CAT_SPAGE_BANNER, ["iname"] = "Page banners"
        });
        int ownImage = add(page);
        int libraryImage = add(0);
        int foreignImage = add(other);
        fw.db.update("att", DB.h("att_categories_id", bannerCategory), DB.h("id", ownImage));
        fw.db.update("att", DB.h("att_categories_id", bannerCategory), DB.h("id", foreignImage));
        int file = add(page, false);
        int deletedImage = add(page, status: FwModel.STATUS_DELETED);
        var state = adminController(request(), "SelectImage").SelectImageAction(page);
        var ids = ((FwList)state["att_dr"]!).Select(row => row["id"].toInt()).ToArray();
        CollectionAssert.AreEquivalent(new[] { ownImage, libraryImage }, ids);
        var bannerRequest = request();
        bannerRequest.FORM["category"] = AttCategories.CAT_SPAGE_BANNER;
        var bannerState = adminController(bannerRequest, "SelectImage").SelectImageAction(page);
        Assert.AreEqual(bannerCategory, bannerState["att_categories_id"].toInt());
        CollectionAssert.AreEquivalent(new[] { ownImage },
            ((FwList)bannerState["att_dr"]!).Select(row => row["id"].toInt()).ToArray());
        Assert.AreEqual("/admin/att/select", state["_basedir"].toStr());
        Assert.AreEqual("/Admin/Spages/(Upload)/" + page, state["upload_url"].toStr());
        Assert.ThrowsExactly<NotFoundException>(() => adminController(request(), "SelectImage").SelectImageAction(999999));
        Assert.ThrowsExactly<AuthException>(() => adminController(request(0), "SelectImage"));
        Assert.ThrowsExactly<AuthException>(() => adminController(request(79), "SelectImage"));
        var draft = cms.oneDraftOrFail(page);
        draft["head_att_id"] = ownImage;
        Assert.IsTrue(cms.getDraftImageUrl(draft).Contains("/Att/"));
        foreach (int unavailable in new[] { foreignImage, file, deletedImage })
        {
            draft["head_att_id"] = unavailable;
            Assert.AreEqual("", cms.getDraftImageUrl(draft));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ImageUploadReturnsStoredUrlAndKeepsPageOwnership(bool isBanner)
    {
        int page = create("actual-upload", access: 80);
        int categoryId = isBanner ? fw.db.insert("att_categories", new FwDict
        {
            ["icode"] = AttCategories.CAT_SPAGE_BANNER, ["iname"] = "Page banners"
        }) : 0;
        string uploadRoot = Path.Combine(Path.GetTempPath(), "osafw-spages-upload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(uploadRoot);
        // FwConfig caches an IConfiguration instance, so use a new provider for this isolated upload root.
        config = new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(
            new System.Collections.Generic.Dictionary<string, string?>
            {
                ["appSettings:site_root"] = uploadRoot,
                ["appSettings:UPLOAD_DIR"] = "/uploads"
            }).Build();
        try
        {
            var current = request();
            current.is_log_events = false; // This fixture isolates uploads and does not install activity-log dictionaries.
            using var stream = File.OpenRead(Path.Combine(root(), "osafw-app/wwwroot/assets/img/logo.png"));
            var file = new Microsoft.AspNetCore.Http.FormFile(stream, 0, stream.Length, "file1", "cms-logo.png");
            current.request.Form = new Microsoft.AspNetCore.Http.FormCollection(
                new System.Collections.Generic.Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
                new Microsoft.AspNetCore.Http.FormFileCollection { file });
            current.Session("XSS", "upload-token");
            current.FORM["XSS"] = "upload-token";
            if (isBanner)
                current.FORM["item"] = new FwDict { ["att_categories_id"] = categoryId };
            var response = (FwDict)adminController(current, "Upload", "POST").UploadAction(page)["_json"]!;
            var attachment = current.db.row("att", DB.h("id", response["id"]));
            Assert.IsTrue(Directory.GetFiles(uploadRoot, "*.png", SearchOption.AllDirectories).Length > 0);
            Assert.IsTrue(response["is_image"].toBool());
            Assert.AreEqual("cms-logo.png", response["iname"].toStr());
            Assert.IsTrue(attachment["icode"].toStr().Length > 0);
            Assert.AreEqual("/Att/" + attachment["icode"].toStr(), response["url"].toStr());
            Assert.AreEqual(page, attachment["item_id"].toInt());
            Assert.AreEqual(categoryId, attachment["att_categories_id"].toInt());
            Assert.AreEqual(current.model<FwEntities>().idByIcode(FwEntities.ICODE_SPAGE), attachment["fwentities_id"].toInt());
        }
        finally
        {
            Directory.Delete(uploadRoot, true);
        }
    }

}
#endif
