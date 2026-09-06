#if isSQLite
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}
#endif
