#if isSQLite
using AngleSharp.Dom;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace osafw.Tests;

public partial class SpagesCmsTests
{
    [TestMethod]
    [DataRow(Spages.STATUS_DRAFT)]
    [DataRow(Spages.STATUS_IN_REVIEW)]
    [DataRow(Spages.STATUS_CHANGES_REQUESTED)]
    [DataRow(Spages.STATUS_SCHEDULED)]
    [DataRow(Spages.STATUS_PUBLISHED)]
    public void PublishedAndWorkingBadgesReflectRealPublicationOnListAndEditor(int status)
    {
        int page = create("dual-status");
        publish(page);
        if (status != Spages.STATUS_PUBLISHED)
        {
            save(page, "Unpublished changes");
            if (status is Spages.STATUS_IN_REVIEW or Spages.STATUS_CHANGES_REQUESTED)
                cms.updateWorkflow(page, "submit");
            if (status == Spages.STATUS_CHANGES_REQUESTED)
                cms.updateWorkflow(page, "changes", "Please revise");
            if (status == Spages.STATUS_SCHEDULED)
                publish(page, DateTime.UtcNow.AddDays(1));
        }
        var current = request();
        current.FORM["f"] = new FwDict { ["s"] = "dual-status" };
        var row = ((FwList)adminController(current).IndexAction()["list_rows"]!).Single();
        var state = adminController(request(), "ShowForm").ShowFormAction(page);
        Assert.IsTrue(row["is_published"].toBool());
        Assert.IsTrue(state["is_published"].toBool());
        Assert.AreEqual("/dual-status", row["view_url"].toStr());
        var parser = new ParsePage(new ParsePageOptions
        {
            TemplatesRoot = Path.Combine(root(), "osafw-app/App_Data/template"), IsLangUpdate = false
        });
        string[] output =
        [
            parser.parse_page("/admin/spages/index", "col_custom.html", new FwDict
            {
                ["field_name"] = "status", ["data"] = row["status"], ["row"] = row
            }),
            parser.parse_page("/admin/spages/showform", "page_header_meta.html", state)
        ];
        foreach (string html in output)
        {
            var badges = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html).QuerySelectorAll(".badge:not([hidden])");
            Assert.HasCount(status == Spages.STATUS_PUBLISHED ? 1 : 2, badges, html);
            Assert.AreEqual("Published", badges[0].TextContent.Trim());
            Assert.IsTrue(badges[0].ClassList.Contains("text-bg-success"));
            if (status != Spages.STATUS_PUBLISHED)
                StringAssert.Contains(badges[1].TextContent, Spages.statusLabel(status));
        }
    }

    [TestMethod]
    public void PublicationIndicatorIncludesRestrictedPagesAndSnippetsButExcludesWithdrawalsAndFutureOnly()
    {
        int restricted = create("status-restricted", access: Users.ACL_SITEADMIN);
        publish(restricted);
        int snippet = cms.saveDraft(0, new FwDict
        {
            ["iname"] = "status-snippet", ["url"] = "status-snippet", ["is_snippet"] = 1,
            ["template"] = "article", ["content_json"] = content("Shared publication")
        });
        publish(snippet);
        int future = create("status-future");
        publish(future, DateTime.UtcNow.AddDays(1));
        int withdrawn = create("status-withdrawn");
        publish(withdrawn);
        cms.updateWorkflow(withdrawn, "unpublish");
        var current = request(Users.ACL_MANAGER);
        current.FORM["f"] = new FwDict { ["s"] = "status-", ["pagesize"] = 25 };
        var rows = (FwList)adminController(current).IndexAction()["list_rows"]!;
        Assert.HasCount(4, rows);
        foreach (var row in rows)
        {
            int id = row["id"].toInt();
            Assert.AreEqual(id == restricted || id == snippet, row["is_published"].toBool());
            Assert.IsFalse(row["is_live"].toBool());
        }
        Assert.ThrowsExactly<AuthException>(() => request(0).model<Spages>().isPublished(restricted));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DiscardDraftRestoresPublishedFieldsAndPreservesPublicationAndHistory(bool isSnippet)
    {
        int page = cms.saveDraft(0, new FwDict
        {
            ["iname"] = "Published title", ["url"] = "discard-source", ["is_snippet"] = isSnippet ? 1 : 0,
            ["template"] = "article", ["content_json"] = content("Published content"),
            ["meta_title"] = "Published SEO", ["is_noindex"] = 1, ["is_nav_visible"] = 1,
            ["custom_css"] = ".example { color: red; }"
        });
        publish(page);
        var publication = cms.onePublished(page);
        var originalRow = fw.db.row("spages", DB.h("id", page));
        cms.saveDraft(page, new FwDict
        {
            ["iname"] = "Discarded title", ["content_json"] = content("Discarded content"),
            ["url"] = isSnippet ? "discard-source" : "discard-working",
            ["meta_title"] = "Draft SEO", ["is_noindex"] = 0, ["is_nav_visible"] = 0
        }, true);
        int historyCount = cms.listRevisions(page).Count;
        var current = request(Users.ACL_MANAGER);
        addXssToken(current, "discard-token");
        var result = adminController(current, "DiscardDraft", "POST").DiscardDraftAction(page);
        Assert.IsTrue(((FwDict)result["_json"]!)["success"].toBool());
        var restored = request().model<Spages>().oneDraftOrFail(page);
        foreach (string field in Utils.qw(Spages.CONTENT_FIELDS))
            Assert.AreEqual(publication[field].toStr(), restored[field].toStr(), field);
        Assert.AreEqual("", restored["review_note"].toStr());
        Assert.AreEqual(originalRow["add_users_id"].toInt(), restored["add_users_id"].toInt());
        Assert.AreEqual(originalRow["add_time"].toStr(), restored["add_time"].toStr());
        var fresh = request().model<Spages>();
        Assert.AreEqual(publication["revision_id"].toInt(), fresh.onePublished(page)["revision_id"].toInt());
        var history = fresh.listRevisions(page);
        Assert.HasCount(historyCount + 1, history);
        Assert.AreEqual("Draft discarded", history[0]["note"].toStr());
        StringAssert.Contains(history[0]["snapshot_json"].toStr(), "Discarded content");
        fresh.updateDiscardDraft(page);
        Assert.AreEqual(history.Count, fresh.listRevisions(page).Count, "A retry must not create another history entry.");
        fresh.restoreRevision(page, history[0]["id"].toInt());
        StringAssert.Contains(fresh.oneDraftOrFail(page)["content_json"].toStr(), "Discarded content");
        Assert.AreEqual(publication["revision_id"].toInt(), fresh.onePublished(page)["revision_id"].toInt());
    }

    [TestMethod]
    public void DiscardDraftRejectsMissingPublicationAndPendingSchedulesWithoutChangingState()
    {
        int draft = create("discard-never-published");
        int withdrawn = create("discard-withdrawn");
        publish(withdrawn);
        cms.updateWorkflow(withdrawn, "unpublish");
        int scheduled = create("discard-scheduled");
        publish(scheduled);
        save(scheduled, "Approved future content");
        publish(scheduled, DateTime.UtcNow.AddDays(1));
        save(scheduled, "Additional changes");
        int deleted = create("discard-deleted");
        publish(deleted);
        cms.delete(deleted);
        foreach (int id in new[] { draft, withdrawn, scheduled, deleted })
        {
            string before = fw.db.value("spages", DB.h("id", id), "draft_json").toStr();
            int count = cms.listRevisions(id).Count;
            Assert.ThrowsExactly<UserException>(() => cms.updateDiscardDraft(id));
            Assert.AreEqual(before, fw.db.value("spages", DB.h("id", id), "draft_json").toStr());
            Assert.AreEqual(count, cms.listRevisions(id).Count);
        }
        Assert.IsTrue(cms.isScheduled(scheduled));
        cms.updateWorkflow(scheduled, "cancel");
        cms.updateDiscardDraft(scheduled);
        Assert.AreEqual(Spages.STATUS_PUBLISHED, cms.oneDraftOrFail(scheduled)["status"].toInt());
        Assert.ThrowsExactly<NotFoundException>(() => cms.updateDiscardDraft(int.MaxValue));
    }

    [TestMethod]
    public void DiscardDraftRequiresPostTokenAndWritableAuthor()
    {
        int page = create("discard-permissions");
        publish(page);
        save(page, "Private draft");
        var get = request();
        Assert.ThrowsExactly<AuthException>(() => adminController(get, "DiscardDraft").DiscardDraftAction(page));
        var noToken = request();
        noToken.Session("XSS", "required-discard-token");
        Assert.ThrowsExactly<AuthException>(() => adminController(noToken, "DiscardDraft", "POST").DiscardDraftAction(page));
        Assert.ThrowsExactly<AuthException>(() => request(Users.ACL_MEMBER).model<Spages>().updateDiscardDraft(page));
        int reader = createReviewUser(Users.ACL_SITEADMIN, true);
        var readOnly = requestForReviewUser(reader, Users.ACL_SITEADMIN);
        addXssToken(readOnly, "readonly-discard");
        Assert.ThrowsExactly<AuthException>(() => adminController(readOnly, "DiscardDraft", "POST").DiscardDraftAction(page));
        Assert.AreEqual(Spages.STATUS_DRAFT, request().model<Spages>().oneDraftOrFail(page)["status"].toInt());
    }

#if isRoles
    [TestMethod]
    public void DiscardDraftRequiresCmsEditPermission()
    {
        int page = create("discard-role");
        publish(page);
        save(page, "Role-protected draft");
        int role = employeeRoleId();
        grantReviewPermission(role, Permissions.PERMISSION_VIEW);
        int manager = createReviewUser(Users.ACL_MANAGER, false, role);
        var denied = requestForReviewUser(manager, Users.ACL_MANAGER);
        addXssToken(denied, "denied-discard");
        Assert.ThrowsExactly<AuthException>(() => adminController(denied, "DiscardDraft", "POST"));
        grantReviewPermission(role, Permissions.PERMISSION_EDIT);
        var allowed = requestForReviewUser(manager, Users.ACL_MANAGER);
        addXssToken(allowed, "allowed-discard");
        adminController(allowed, "DiscardDraft", "POST").DiscardDraftAction(page);
        Assert.AreEqual(Spages.STATUS_PUBLISHED, request().model<Spages>().oneDraftOrFail(page)["status"].toInt());
    }
#endif
}
#endif
