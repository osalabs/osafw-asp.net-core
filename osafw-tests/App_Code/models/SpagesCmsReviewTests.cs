#if isSQLite
using AngleSharp.Dom;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace osafw.Tests;

public partial class SpagesCmsTests
{
    private AdminSpagesController reviewController(FW current, string action, string method = "GET", int id = 0, string actionMore = "")
    {
        current.setController("AdminSpages", action);
        current.route.method = method;
        current.route.id = id > 0 ? id.ToString() : "";
        current.route.action_more = actionMore;
        var controller = new AdminSpagesController();
        controller.init(current);
        return controller;
    }

    private static void addXssToken(FW current, string token)
    {
        current.Session("XSS", token);
        current.FORM["XSS"] = token;
    }

    [TestMethod]
    public void RenderedCmsControlsKeepAccessLevelsAndApplicationRootOnLinks()
    {
        int page = create("rooted-page", access: Users.ACL_MANAGER);
        var current = request();
        var controller = reviewController(current, FW.ACTION_SHOW_FORM, id: page, actionMore: FW.ACTION_MORE_EDIT);
        controller.checkAccess();
        var state = controller.ShowFormAction(page);
        var parser = new ParsePage(new ParsePageOptions
        {
            TemplatesRoot = Path.Combine(root(), "osafw-app/App_Data/template"),
            IsLangUpdate = false,
            GlobalsGetter = () => new FwDict { ["ROOT_URL"] = "/portal" }
        });

        string formHtml = parser.parse_page("/admin/spages/showform", "main.html", state);
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(formHtml);
        var publishButton = document.QuerySelector(".page-header [data-publish-now]")
            ?? throw new AssertFailedException("Publishers need the header Publish action.");
        Assert.IsTrue(publishButton.ClassList.Contains("btn-success"));
        Assert.IsFalse(publishButton.HasAttribute("disabled"));
        Assert.AreEqual("spages-editor", publishButton.PreviousElementSibling?.GetAttribute("form"));
        Assert.AreEqual("Draft", document.QuerySelector(".page-header #spages-status")?.TextContent);

        var accessSelect = document.QuerySelector("select[name='item[access_level]']")
            ?? throw new AssertFailedException("The CMS access-level select was not rendered.");
        var options = accessSelect.QuerySelectorAll("option");
        CollectionAssert.AreEqual(new[] { "0", "1", "50", "80", "90", "100" },
            options.Select(option => option.GetAttribute("value") ?? "").ToArray());
        Assert.AreEqual("80", options.Single(option => option.HasAttribute("selected")).GetAttribute("value"));

        string columnHtml = parser.parse_page("/admin/spages/index", "col_custom.html", new FwDict
        {
            ["field_name"] = "url",
            ["data"] = "rooted-page",
            ["row"] = new FwDict { ["full_url"] = "/rooted-page", ["redirect_url"] = "" }
        });
        string actionsHtml = parser.parse_page("/admin/spages/index", "list_row_btn.html", new FwDict
        {
            ["row_click_url"] = "/portal/Admin/Spages/" + page + "/edit",
            ["full_url"] = "/rooted-page"
        });
        StringAssert.Contains(columnHtml, "href=\"/portal/rooted-page\"");
        StringAssert.Contains(actionsHtml, "href=\"/portal/rooted-page\"");
        Assert.IsFalse(columnHtml.Contains("href=\"/rooted-page\"", StringComparison.Ordinal));
        Assert.IsFalse(actionsHtml.Contains("href=\"/rooted-page\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ReadOnlyEditorsCanUseAuthorizedReadRoutesButCannotSaveDrafts()
    {
        int page = create("readonly-route");
        int readOnlySiteAdmin = createReviewUser(Users.ACL_SITEADMIN, true);
        var siteAdminIndexFw = requestForReviewUser(readOnlySiteAdmin, Users.ACL_SITEADMIN);
        var siteAdminIndex = reviewController(siteAdminIndexFw, FW.ACTION_INDEX);
        siteAdminIndex.checkAccess();
        Assert.IsTrue(siteAdminIndex.IndexAction()["is_readonly"].toBool());
        var siteAdminFormFw = requestForReviewUser(readOnlySiteAdmin, Users.ACL_SITEADMIN);
        var siteAdminForm = reviewController(siteAdminFormFw, FW.ACTION_SHOW_FORM, id: page, actionMore: FW.ACTION_MORE_EDIT);
        siteAdminForm.checkAccess();
        var readOnlyState = siteAdminForm.ShowFormAction(page);
        Assert.IsTrue(readOnlyState["is_readonly"].toBool());
        var parser = new ParsePage(new ParsePageOptions
        {
            TemplatesRoot = Path.Combine(root(), "osafw-app/App_Data/template"),
            IsLangUpdate = false
        });
        string readOnlyActions = parser.parse_page("/admin/spages/showform", "page_header_actions_right.html", readOnlyState);
        Assert.IsFalse(readOnlyActions.Contains("data-publish-now", StringComparison.Ordinal),
            "A read-only Site Admin must not receive the header Publish action.");
        Assert.ThrowsExactly<AuthException>(() => siteAdminFormFw.model<Spages>().saveDraft(page, new FwDict { ["iname"] = "Denied" }));

#if isRoles
        int roleId = employeeRoleId();
        grantReviewPermission(roleId, Permissions.PERMISSION_LIST);
        grantReviewPermission(roleId, Permissions.PERMISSION_EDIT);
        int readOnlyManager = createReviewUser(Users.ACL_MANAGER, true, roleId);
        var managerIndexFw = requestForReviewUser(readOnlyManager, Users.ACL_MANAGER);
        var managerIndex = reviewController(managerIndexFw, FW.ACTION_INDEX);
        managerIndex.checkAccess();
        Assert.IsTrue(managerIndex.IndexAction()["is_readonly"].toBool());
        var managerFormFw = requestForReviewUser(readOnlyManager, Users.ACL_MANAGER);
        var managerForm = reviewController(managerFormFw, FW.ACTION_SHOW_FORM, id: page, actionMore: FW.ACTION_MORE_EDIT);
        managerForm.checkAccess();
        Assert.IsTrue(managerForm.ShowFormAction(page)["is_readonly"].toBool());
        Assert.ThrowsExactly<AuthException>(() => managerFormFw.model<Spages>().saveDraft(page, new FwDict { ["iname"] = "Denied" }));

        var managerNewFw = requestForReviewUser(readOnlyManager, Users.ACL_MANAGER);
        Assert.ThrowsExactly<AuthException>(() => reviewController(managerNewFw, FW.ACTION_SHOW_FORM).checkAccess(),
            "A new form must use the add permission rather than inheriting edit access.");
        grantReviewPermission(roleId, Permissions.PERMISSION_ADD);
        var managerAllowedNewFw = requestForReviewUser(readOnlyManager, Users.ACL_MANAGER);
        var managerAllowedNew = reviewController(managerAllowedNewFw, FW.ACTION_SHOW_FORM);
        managerAllowedNew.checkAccess();
        Assert.IsTrue(managerAllowedNew.ShowFormAction()["is_readonly"].toBool());
#endif

        var lowLevel = request(Spages.AUTHOR_LEVEL - 1);
        lowLevel.setController("AdminSpages", FW.ACTION_INDEX);
        Assert.ThrowsExactly<AuthException>(() => lowLevel.model<Spages>().listSelectOptionsParents(0));
        var anonymous = request(0);
        anonymous.setController("AdminSpages", FW.ACTION_SHOW_FORM);
        Assert.ThrowsExactly<AuthException>(() => anonymous.model<Spages>().listSelectOptionsSnippets());
    }

    [TestMethod]
    public void EditorBreadcrumbsUseDraftAncestryAndStandardRecordNavigation()
    {
        int rootPage = create("breadcrumb-root");
        int parent = create("breadcrumb-parent", rootPage);
        int child = create("breadcrumb-child", parent);
        cms.saveDraft(rootPage, new FwDict { ["iname"] = "Draft <root>" });
        var current = request();
        current.FORM["parent_id"] = parent;
        var controller = reviewController(current, FW.ACTION_SHOW_FORM);
        controller.checkAccess();
        var state = controller.ShowFormAction();
        CollectionAssert.AreEqual(new[] { rootPage, parent }, ((FwList)state["ancestors"]!).Select(row => row["id"].toInt()).ToArray());
        Assert.AreEqual(0, request(0).model<Spages>().onePublished(parent).Count);
        var parser = new ParsePage(new ParsePageOptions
        {
            TemplatesRoot = Path.Combine(root(), "osafw-app/App_Data/template"), IsLangUpdate = false
        });
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(parser.parse_page("/admin/spages/showform", "main.html", state));
        var links = document.QuerySelectorAll(".page-header-breadcrumbs a");
        Assert.AreEqual("/Admin/Spages/" + rootPage + "/edit", links[1].GetAttribute("href"));
        Assert.AreEqual("Draft <root>", links[1].TextContent);
        Assert.IsNull(links[1].QuerySelector("root"));
        Assert.AreEqual("/Admin/Spages/" + parent + "/edit", links[2].GetAttribute("href"));
        Assert.IsNotNull(document.QuerySelector(".page-header-record-nav .on-fw-quick-search"));
        Assert.IsNull(document.QuerySelector(".page-header-record-nav a"), "An unsaved subpage has no adjacent record yet.");

        var existing = reviewController(request(), FW.ACTION_SHOW_FORM, id: child, actionMore: FW.ACTION_MORE_EDIT).ShowFormAction(child);
        document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(parser.parse_page("/admin/spages/showform", "main.html", existing));
        Assert.AreEqual(2, document.QuerySelectorAll(".page-header-record-nav a[href*='/(Next)/']").Length);
        CollectionAssert.AreEqual(new[] { rootPage, parent }, ((FwList)existing["ancestors"]!).Select(row => row["id"].toInt()).ToArray());
    }

    [TestMethod]
    public void EditorQuickSearchIncludesDraftsAndUsesListPermissionForReadOnlyManagers()
    {
        int draft = create("quick-draft");
        int deleted = create("quick-deleted");
        cms.saveDraft(draft, new FwDict { ["iname"] = "Quick draft" });
        cms.saveDraft(deleted, new FwDict { ["iname"] = "Quick deleted" });
        cms.updateWorkflow(deleted, "delete");
        var current = request();
        current.FORM["q"] = "Quick";
        var choices = (StrList)adminController(current, "QuickSearch").QuickSearchAction()["_json"]!;
        CollectionAssert.AreEqual(new[] { FormUtils.formatAutocomplete("Quick draft", draft.ToString()) }, choices.ToArray());
        Assert.AreEqual(draft, cms.listSelectOptionsAutocomplete(draft.ToString())[0]["id"].toInt());
        Assert.ThrowsExactly<AuthException>(() => request(0).model<Spages>().listSelectOptionsAutocomplete("Quick"));

#if isRoles
        int role = employeeRoleId();
        grantReviewPermission(role, Permissions.PERMISSION_LIST);
        int reader = createReviewUser(Users.ACL_MANAGER, true, role);
        var lookup = requestForReviewUser(reader, Users.ACL_MANAGER);
        lookup.FORM["q"] = "Quick";
        Assert.AreEqual(1, ((StrList)adminController(lookup, "QuickSearch").QuickSearchAction()["_json"]!).Count);
        var go = requestForReviewUser(reader, Users.ACL_MANAGER);
        go.FORM["s"] = choices[0];
        go.FORM["is_edit"] = "1";
        Assert.AreEqual("/Admin/Spages/" + draft + "/edit", adminController(go, "Go").GoAction()["_redirect"].toStr());
        Assert.ThrowsExactly<AuthException>(() => go.model<Spages>().saveDraft(draft, new FwDict { ["iname"] = "Denied" }));
#endif
    }

    [TestMethod]
    public void SnippetFormsDisablePageOnlySettingsAndKeepPageRedirectControls()
    {
        int page = create("settings-page");
        cms.saveDraft(page, new FwDict { ["redirect_url"] = "/Contact", ["url_aliases"] = "/old-settings-page" });
        int snippet = cms.saveDraft(0, new FwDict
        {
            ["iname"] = "Associated help", ["is_snippet"] = 1, ["url"] = "settings-snippet",
            ["parent_id"] = page, ["template"] = "article", ["content_json"] = content("Help text")
        });
        var parser = new ParsePage(new ParsePageOptions
        {
            TemplatesRoot = Path.Combine(root(), "osafw-app/App_Data/template"), IsLangUpdate = false
        });
        foreach (int id in new[] { page, snippet })
        {
            var state = reviewController(request(), FW.ACTION_SHOW_FORM, id: id, actionMore: FW.ACTION_MORE_EDIT).ShowFormAction(id);
            var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(parser.parse_page("/admin/spages/showform", "main.html", state));
            foreach (string selector in new[] { "#spages-nav-title", "#spages-meta-title", "#spages-noindex", "#spages-redirect-url", "#spages-url-aliases", "#spages-select-image", "#spages-custom-css" })
            {
                var field = document.QuerySelector(selector) ?? throw new AssertFailedException("Missing setting " + selector);
                Assert.AreEqual(id == snippet, field.Closest("fieldset")?.HasAttribute("disabled") == true, selector);
            }
            Assert.AreEqual(id == snippet, document.QuerySelector("#spages-snippet-help") != null);
            Assert.IsNull(document.QuerySelector("#spages-image-pane .fw-fieldset-legend, #spages-custom-pane .fw-fieldset-legend"));
            Assert.IsNull(document.QuerySelector("#spages-access-level")?.Closest("fieldset[disabled]"));
            if (id == page)
            {
                Assert.AreEqual("/Contact", document.QuerySelector("#spages-redirect-url")?.GetAttribute("value"));
                Assert.AreEqual("/old-settings-page", document.QuerySelector("#spages-url-aliases")?.TextContent);
            }
        }
    }

    [TestMethod]
    public void UnicodePublicationLookupsMatchOrdinalCaseAndKeepNegativeBoundaries()
    {
        int published = create("Équipe");
        publish(published);
        int draft = create("Brouillon");
        int restricted = create("Réservé", access: Users.ACL_MANAGER);
        publish(restricted);

        var anonymous = request(0).model<Spages>();
        Assert.AreEqual(published, anonymous.oneByUrl("équipe", 0)["id"].toInt());
        Assert.AreEqual(published, anonymous.onePublishedByPath("/équipe")["id"].toInt());
        Assert.AreEqual(0, anonymous.oneByUrl("équipe", published).Count, "A matching slug under the wrong parent must stay hidden.");
        Assert.AreEqual(0, anonymous.oneByUrl("brouillon", 0).Count, "Drafts must not become public through Unicode lookup.");
        Assert.AreEqual(0, anonymous.onePublishedByPath("/brouillon").Count);
        Assert.AreEqual(0, anonymous.oneByUrl("réservé", 0).Count, "Audience restrictions must remain enforced.");
        Assert.AreEqual(0, anonymous.onePublishedByPath("/réservé").Count);

        var manager = request(Users.ACL_MANAGER).model<Spages>();
        Assert.AreEqual(restricted, manager.oneByUrl("réservé", 0)["id"].toInt());
        Assert.AreEqual(restricted, manager.onePublishedByPath("/réservé")["id"].toInt());
        Assert.AreEqual(draft, cms.oneDraftOrFail(draft)["id"].toInt(), "The negative draft control must still exist as editor data.");
    }

#if isRoles
    [TestMethod]
    public void ManagerListAndEditRoleCannotDeleteUntilDeletePermissionIsGranted()
    {
        int page = create("role-delete-gate");
        publish(page);
        int roleId = employeeRoleId();
        grantReviewPermission(roleId, Permissions.PERMISSION_LIST);
        grantReviewPermission(roleId, Permissions.PERMISSION_EDIT);
        int managerId = createReviewUser(Users.ACL_MANAGER, false, roleId);

        var accessFw = requestForReviewUser(managerId, Users.ACL_MANAGER);
        var users = accessFw.model<Users>();
        Assert.IsTrue(users.isAccessByRolesResourcePermission(managerId, "AdminSpages", Permissions.PERMISSION_LIST));
        Assert.IsTrue(users.isAccessByRolesResourcePermission(managerId, "AdminSpages", Permissions.PERMISSION_EDIT));
        Assert.IsFalse(users.isAccessByRolesResourcePermission(managerId, "AdminSpages", Permissions.PERMISSION_VIEW));
        Assert.IsFalse(users.isAccessByRolesResourcePermission(managerId, "AdminSpages", Permissions.PERMISSION_DELETE));
        var index = reviewController(accessFw, FW.ACTION_INDEX);
        index.checkAccess();
        Assert.IsTrue(index.IndexAction()["count"].toInt() > 0, "List access must not require the separate view permission.");
        var editFw = requestForReviewUser(managerId, Users.ACL_MANAGER);
        var edit = reviewController(editFw, FW.ACTION_SHOW_FORM, id: page, actionMore: FW.ACTION_MORE_EDIT);
        edit.checkAccess();
        Assert.AreEqual(page, ((FwDict)edit.ShowFormAction(page)["i"]!)["id"].toInt(),
            "Edit access must not require the separate view permission.");

        assertBulkDeleteDenied(managerId, page, false);
        assertBulkDeleteDenied(managerId, page, true);
        Assert.AreEqual(Spages.STATUS_PUBLISHED, cms.oneDraftOrFail(page)["status"].toInt());
        Assert.AreEqual(page, request(0).model<Spages>().onePublished(page)["id"].toInt());

        grantReviewPermission(roleId, Permissions.PERMISSION_DELETE);
        var deleteFw = bulkDeleteRequest(managerId, page, true);
        var delete = reviewController(deleteFw, FW.ACTION_SAVE_MULTI, "PUT");
        delete.checkAccess();
        deleteFw.request.Headers.Accept = "application/json";
        delete.SaveMultiAction();
        Assert.AreEqual(Spages.STATUS_DELETED, cms.oneDraftOrFail(page)["status"].toInt());
        Assert.AreEqual(0, request(0).model<Spages>().onePublished(page).Count);

        var restoreFw = requestForReviewUser(managerId, Users.ACL_MANAGER);
        addXssToken(restoreFw, "review-restore-token");
        restoreFw.request.Headers.Accept = "application/json";
        var restore = reviewController(restoreFw, FW.ACTION_DELETE_RESTORE, "POST", page);
        restore.checkAccess();
        restore.RestoreDeletedAction(page);
        Assert.AreEqual(Spages.STATUS_DRAFT, cms.oneDraftOrFail(page)["status"].toInt());
        Assert.AreEqual(0, request(0).model<Spages>().onePublished(page).Count,
            "Restoring a deleted page must not republish its earlier release.");
    }

    private void assertBulkDeleteDenied(int managerId, int page, bool isCraftedCmsAction)
    {
        var checkedFw = bulkDeleteRequest(managerId, page, isCraftedCmsAction);
        var checkedController = reviewController(checkedFw, FW.ACTION_SAVE_MULTI, "PUT");
        Assert.ThrowsExactly<AuthException>(() => checkedController.checkAccess());
        Assert.AreEqual(Spages.STATUS_PUBLISHED, cms.oneDraftOrFail(page)["status"].toInt());

        var directFw = bulkDeleteRequest(managerId, page, isCraftedCmsAction);
        var directController = reviewController(directFw, FW.ACTION_SAVE_MULTI, "PUT");
        Assert.ThrowsExactly<AuthException>(() => directController.SaveMultiAction());
        Assert.AreEqual(Spages.STATUS_PUBLISHED, cms.oneDraftOrFail(page)["status"].toInt());
    }

    private FW bulkDeleteRequest(int managerId, int page, bool isCraftedCmsAction)
    {
        var current = requestForReviewUser(managerId, Users.ACL_MANAGER);
        addXssToken(current, "review-delete-token");
        current.FORM["cb"] = new FwDict { [page.ToString()] = 1 };
        if (isCraftedCmsAction)
            current.FORM["cms_action"] = "delete";
        else
            current.FORM["delete"] = "Delete";
        return current;
    }

    private int employeeRoleId()
    {
        int roleId = fw.db.value("roles", new FwDict { ["iname"] = "Employee" }, "id").toInt();
        Assert.IsTrue(roleId > 0, "The roles fixture must seed the Employee role.");
        return roleId;
    }

    private void grantReviewPermission(int roleId, string permissionCode)
    {
        int resourceId = fw.db.value("resources", new FwDict { ["icode"] = "AdminSpages" }, "id").toInt();
        int permissionId = fw.db.value("permissions", new FwDict { ["icode"] = permissionCode }, "id").toInt();
        Assert.IsTrue(resourceId > 0 && permissionId > 0, "The roles fixture must seed AdminSpages and " + permissionCode + ".");
        fw.db.insert("roles_resources_permissions", new FwDict
        {
            ["roles_id"] = roleId,
            ["resources_id"] = resourceId,
            ["permissions_id"] = permissionId,
            ["status"] = FwModel.STATUS_ACTIVE
        });
    }
#endif

    private int createReviewUser(int accessLevel, bool isReadOnly, int roleId = 0)
    {
        int userId = fw.db.insert("users", new FwDict
        {
            ["fname"] = "CMS",
            ["lname"] = "Reviewer",
            ["email"] = "cms-review-" + Guid.NewGuid().ToString("N") + "@example.test",
            ["pwd"] = "test-only",
            ["access_level"] = accessLevel,
            ["is_readonly"] = isReadOnly ? 1 : 0,
            ["status"] = FwModel.STATUS_ACTIVE
        });
#if isRoles
        if (roleId > 0)
        {
            fw.db.insert("users_roles", new FwDict
            {
                ["users_id"] = userId,
                ["roles_id"] = roleId,
                ["status"] = FwModel.STATUS_ACTIVE
            });
        }
#endif
        return userId;
    }

    private FW requestForReviewUser(int userId, int accessLevel)
    {
        var current = request(accessLevel);
        current.Session("user_id", userId.ToString());
        return current;
    }
}
#endif
