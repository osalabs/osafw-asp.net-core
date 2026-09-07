// Static Pages Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com
using System;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace osafw;

public class AdminSpagesController : FwDynamicController
{
    public static new int access_level = Users.ACL_MEMBER;
    protected Spages model = null!;
    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Admin/Spages";
        loadControllerConfig();
        model = model0 as Spages ?? throw new FwConfigUndefinedModelException();
        db = model.getDB();
    }

    public override void setListSearch()
    {
        if (string.IsNullOrEmpty(list_view) || list_view == model.table_name)
        {
            list_view = model.adminListSource(list_where_params);
        }

        base.setListSearch();
        string kind = list_filter["is_snippet"].toStr();
        if (kind is "0" or "1")
        {
            list_where += " AND is_snippet=@spages_kind";
            list_where_params["spages_kind"] = kind.toInt();
        }
    }

    protected override void setListFields()
    {
        var fields = Utils.qw(getViewListUserFields()).Where(view_list_map.ContainsKey)
            .Concat(Utils.qw("id parent_id iname url status is_snippet is_home redirect_url")).Distinct();
        list_fields = string.Join(", ", fields.Select(field => db.qid(field)));
    }

    public override void setListSearchStatus()
    {
        if (list_filter["status"].toStr() == "all" && fw.userAccessLevel >= Users.ACL_SITEADMIN)
        {
            return;
        }

        base.setListSearchStatus();
    }

    public override DBList getListRowsQuery(int offset = 0, int limit = -1)
    {
        if (list_filter["sortby"].toStr() != "iname")
        {
            return base.getListRowsQuery(offset, limit);
        }

        // Apply SQL filters first, then page the hierarchy so children stay below their parent.
        // Only the selected list columns are read; content documents are never loaded here.
        var rows = model.listAdminTreeRows(base.getListRowsQuery());
        var page = rows.Skip(offset);
        return new DBList(limit >= 0 ? page.Take(limit) : page);
    }

    /// <summary>Previous/Next follows the same filtered hierarchy as the Title-sorted list.</summary>
    public override StrList getListIds(string list_view = "")
    {
        if (list_filter["sortby"].toStr() != "iname")
            return base.getListIds(list_view);

        list_fields = "id, parent_id, iname, is_snippet";
        return new StrList(getListRowsQuery().Select(row => row["id"].toStr()));
    }

    public override void getListRows()
    {
        base.getListRows();
        model.attachPublicationUrls(list_rows);
        foreach (var row in list_rows)
        {
            row["status_label"] = Spages.statusLabel(row["status"].toInt());
            row["view_url"] = row["is_live"].toBool() ? row["full_url"] : base_url + "/(Preview)/" + row["id"];
            row["view_title"] = row["is_live"].toBool() ? "View published page" : "Preview draft";
        }
    }

    public override FwDict modelOne(int id) => id > 0 ? model.oneDraftOrFail(id) : new FwDict();

    public override FwDict ShowAction(int id) => new FwDict { ["_redirect"] = base_url + "/" + id + "/edit" };

    public override FwDict ShowFormAction(int id = 0)
    {
        return buildForm(id);
    }

    public override FwDict? SaveAction(int id = 0)
    {
        return saveForm(id);
    }

    public override void Validate(int id, FwDict item)
    {
        bool isValid = this.validateRequired(id, item, this.required_fields);
        if (isValid && model.isExistsByUrl(item["url"].toStr(), item["parent_id"].toInt(), id))
        {
            fw.FormErrors["url"] = "EXISTS";
        }

        var redirect_url = item["redirect_url"].toStr();
        if (isValid && !Utils.isEmpty(redirect_url) && !Utils.isAppUrl(redirect_url, fw.config("ROOT_DOMAIN").toStr()))
        {
            fw.FormErrors["redirect_url"] = "APP_URL";
        }

        if (isValid)
        {
            // Prevent setting parent_id to itself or its descendants
            int parent_id = item["parent_id"].toInt();
            if (id > 0 && parent_id > 0)
            {
                if (parent_id == id)
                {
                    fw.FormErrors["parent_id"] = true;
                    throw new UserException("Page cannot be its own parent");
                }

                // Check if parent_id is a descendant of current page
                var parentChain = model.listParents(parent_id);
                foreach (FwDict parentItem in parentChain)
                {
                    if (parentItem["id"].toInt() == id)
                    {
                        fw.FormErrors["parent_id"] = true;
                        throw new UserException("Page cannot be a parent of its own descendant");
                    }
                }
            }
        }

        this.validateCheckResult();
    }

    public override void checkAccess()
    {
        if (fw.userId <= 0 || fw.userAccessLevel < Spages.AUTHOR_LEVEL)
        {
            throw new AuthException();
        }

        if (fw.route.action == "SaveSort")
        {
            throw new UserException("Use the CMS draft and publication controls to change pages.");
        }

        // Existing RBAC remains an additional restriction; workflow roles never grant a missing resource permission.
        access_actions_to_permissions = new()
        {
            ["QuickSearch"] = Permissions.PERMISSION_LIST,
            ["Go"] = Permissions.PERMISSION_LIST,
            ["Publish"] = Permissions.PERMISSION_EDIT,
            ["Submit"] = Permissions.PERMISSION_EDIT,
            ["Changes"] = Permissions.PERMISSION_EDIT,
            ["Unpublish"] = Permissions.PERMISSION_EDIT,
            ["Cancel"] = Permissions.PERMISSION_EDIT,
            ["DiscardDraft"] = Permissions.PERMISSION_EDIT,
            ["RestoreRevision"] = Permissions.PERMISSION_EDIT,
            ["Upload"] = Permissions.PERMISSION_EDIT,
            ["Preview"] = Permissions.PERMISSION_VIEW,
            ["Migrate"] = Permissions.PERMISSION_EDIT,
            ["Media"] = Permissions.PERMISSION_VIEW,
            ["SelectImage"] = Permissions.PERMISSION_VIEW
        };
        if (fw.route.action == "SaveMulti" && bulkAction() is "delete" or "restore")
        {
            // Standard RBAC maps SaveMulti to edit before applying controller overrides.
            access_actions_to_permissions[Permissions.PERMISSION_EDIT] = Permissions.PERMISSION_DELETE;
        }
        base.checkAccess();
    }

    public override FwDict IndexAction()
    {
        var ps = base.IndexAction();
        if (fw.isJsonExpected())
        {
            return ps;
        }

        ps["is_needs_migration"] = db.valuep("SELECT COUNT(*) FROM " + db.qid(model.table_name)
            + " WHERE draft_json IS NULL OR draft_json=''").toInt() > 0;
        ps["is_site_admin"] = fw.userAccessLevel >= Users.ACL_SITEADMIN;
        ps["is_author"] = model.isAuthor();
        ps["is_publisher"] = model.isPublisher();
        ps["is_readonly"] = is_readonly || list_filter["status"].toStr() == "127";
        return ps;
    }

    private FwDict buildForm(int id)
    {
        var item = id > 0 ? model.oneDraftOrFail(id) : new FwDict
        {
            ["iname"] = "",
            ["parent_id"] = reqi("parent_id"),
            ["is_snippet"] = reqi("snippet"),
            ["template"] = "article",
            ["content_json"] = SpagesContent.empty().ToJsonString(),
            ["is_nav_visible"] = 1,
            ["status"] = Spages.STATUS_DRAFT
        };
        item["status_label"] = Spages.statusLabel(item["status"].toInt());
        var parentOptions = model.listSelectOptionsParents(id);
        var snippets = model.listSelectOptionsSnippets();
        string parentPath = parentOptions.FirstOrDefault(row => row["id"].toInt() == item["parent_id"].toInt())?["full_url"].toStr() ?? "";

        var layouts = new FwList();
        foreach (var entry in SpagesContent.layouts())
        {
            if (!item["is_snippet"].toBool() || entry.Key == "article")
            {
                layouts.Add(new FwDict { ["id"] = entry.Key, ["title"] = entry.Value.Title });
            }
        }

        FwDict ps = new FwDict
        {
            ["cms"] = true,
            ["id"] = id,
            ["i"] = item,
            ["parent_url_prefix"] = fw.config("ROOT_DOMAIN").toStr().TrimEnd('/') + parentPath.TrimEnd('/') + "/",
            ["head_image_url"] = model.getDraftImageUrl(item),
            ["parent_options"] = parentOptions,
            ["ancestors"] = model.listDraftParents(item["parent_id"].toInt()),
            ["layouts"] = layouts,
            ["layouts_json"] = Utils.jsonEncode(SpagesContent.layouts().ToDictionary(x => x.Key, x => new { regions = x.Value.Regions, slots = x.Value.Slots })),
            ["snippets_json"] = Utils.jsonEncode(snippets),
            ["history"] = id > 0 ? model.listRevisions(id) : new FwList(),
            ["is_publisher"] = model.isPublisher(),
            ["is_readonly"] = !model.isAuthor() || item["status"].toInt() == Spages.STATUS_DELETED,
            ["base_url"] = base_url,
            ["rbac"] = rbac,
            ["is_showform"] = true,
            ["full_url"] = id > 0 ? model.publishedUrl(id) : "",
            ["usages"] = item["is_snippet"].toBool() ? model.listSnippetUsages(item["url"].toStr(), true) : new FwList()
        };
        ps["is_site_admin"] = fw.model<Users>().isAccessLevel(Users.ACL_SITEADMIN);
        ps["is_author"] = model.isAuthor() && item["status"].toInt() != Spages.STATUS_DELETED;
        ps["is_published"] = model.isPublished(id);
        ps["is_scheduled"] = id > 0 && model.isScheduled(id);
        ps["is_live"] = id > 0 && model.onePublished(id, Users.ACL_SITEADMIN).Count > 0;
        ps["view_url"] = id > 0 ? model.onePublished(id)["full_url"].toStr() : "";
        ps["is_in_review"] = item["status"].toInt() == Spages.STATUS_IN_REVIEW;
        setAddUpdUser(ps, item);
        setPSReturnContext(ps);
        return ps;
    }

    private FwDict saveForm(int id)
    {
        enforcePost();
        checkReadOnly();
        int savedId = model.saveDraft(id, reqh("item"), reqb("autosave"));
        return new FwDict
        {
            ["_json"] = new FwDict
            {
                ["success"] = true,
                ["id"] = savedId,
                ["is_published"] = model.isPublished(savedId),
                ["is_scheduled"] = model.isScheduled(savedId),
                ["location"] = base_url + "/" + savedId + "/edit"
            }
        };
    }

    private FwDict workflow(int id, string action)
    {
        enforcePost();
        checkReadOnly();
        DateTime? publishAt = null;
        string date = reqs("publish_at");
        if (action == "publish" && date.Length > 0)
        {
            if (!DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                throw new UserException("Enter a valid publication date and time.");
            }

            publishAt = parsed.UtcDateTime;
        }

        int revisionId = model.updateWorkflow(id, action, reqs("note"), publishAt);
        var item = model.oneDraftOrFail(id);
        return new FwDict
        {
            ["_json"] = new FwDict
            {
                ["success"] = true,
                ["revision_id"] = revisionId,
                ["status"] = item["status"],
                ["status_label"] = Spages.statusLabel(item["status"].toInt())
            }
        };
    }

    public FwDict PublishAction(int id) => workflow(id, "publish");

    public FwDict SubmitAction(int id) => workflow(id, "submit");

    public FwDict ChangesAction(int id) => workflow(id, "changes");

    public FwDict UnpublishAction(int id) => workflow(id, "unpublish");

    public FwDict CancelAction(int id) => workflow(id, "cancel");

    public FwDict DiscardDraftAction(int id)
    {
        enforcePost();
        checkReadOnly();
        model.updateDiscardDraft(id);
        return new FwDict { ["_json"] = new FwDict { ["success"] = true } };
    }

    public FwDict RestoreRevisionAction(int id)
    {
        enforcePost();
        checkReadOnly();
        model.restoreRevision(id, reqi("revision_id"));
        return new FwDict
        {
            ["_json"] = new FwDict
            {
                ["success"] = true
            }
        };
    }

    public void PreviewAction(int id)
    {
        if (!model.isPreviewAllowed())
        {
            throw new AuthException();
        }

        int revisionId = reqi("revision_id");
        var item = revisionId > 0 ? model.oneRevisionOrFail(id, revisionId) : model.oneDraftOrFail(id);
        var breadcrumbs = new FwList(model.listDraftParents(item["parent_id"].toInt())
            .Where(parent => !parent["is_home"].toBool())
            .Select(parent => new FwDict
            {
                ["iname"] = parent["iname"],
                ["url"] = base_url + "/(Preview)/" + parent["id"]
            }));
        if (!item["is_home"].toBool())
        {
            breadcrumbs.Add(new FwDict { ["iname"] = item["iname"], ["is_current"] = true });
        }
        item["breadcrumbs"] = breadcrumbs;
        fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_PUBLIC");
        fw.cache_control = "private, no-store";
        fw.response.Headers.CacheControl = fw.cache_control;
        fw.response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        fw.parser("/home/spage", model.buildPageState(item, true, revisionId > 0));
    }

    public override FwDict? DeleteAction(int id)
    {
        enforceListMutation("DELETE");
        requireDeletePermission();
        model.delete(id);
        fw.flash("onedelete", 1);
        return afterSave(true);
    }

    public override FwDict? RestoreDeletedAction(int id)
    {
        enforcePost();
        checkReadOnly();
        requireDeletePermission();
        model.updateRestoreDeleted(id);
        fw.flash("record_updated", 1);
        return afterSave(true, id);
    }

    /// <summary>Standard list forms use a PUT method override; CMS transitions still require the session XSS token.</summary>
    public override FwDict? SaveMultiAction()
    {
        enforceListMutation("PUT");
        route_onerror = FW.ACTION_INDEX;
        string action = bulkAction();
        if (action is "delete" or "restore")
        {
            requireDeletePermission();
        }
        if (action.Length == 0)
        {
            return base.SaveMultiAction(); // Standard user-list operations.
        }

        if (action is not ("submit" or "unpublish" or "delete" or "restore"))
        {
            throw new UserException("Choose a supported page action.");
        }

        model.requireAuthor(action != "submit");
        var rows = new FwList();
        foreach (string key in reqh("cb").Keys)
        {
            if (!int.TryParse(key, out int id) || id <= 0)
            {
                throw new UserException("Select valid pages.");
            }

            var item = model.oneDraftOrFail(id);
            if (action == "delete" && item["is_home"].toBool())
            {
                throw new UserException("The home page cannot be deleted.");
            }

            bool isDeleted = item["status"].toInt() == Spages.STATUS_DELETED;
            if (isDeleted != (action == "restore"))
            {
                throw new UserException("Restore pages from the trash before editing them.");
            }

            rows.Add(item);
        }

        int count = 0;
        try
        {
            // Withdraw pages before shared snippets so dependency validation sees each committed transition.
            foreach (var item in rows.OrderBy(x => x["is_snippet"].toInt()))
            {
                int id = item["id"].toInt();
                if (action == "restore")
                {
                    model.updateRestoreDeleted(id);
                }
                else
                {
                    model.updateWorkflow(id, action, "Updated through the page list");
                }

                count++;
            }
        }
        catch (UserException ex)
        {
            throw new UserException($"{count} page(s) updated. {ex.Message}");
        }

        fw.flash("success", $"{count} page(s) updated.");
        return afterSave(true, new FwDict { ["ctr"] = count });
    }

    private void enforceListMutation(string method)
    {
        if (fw.route.method != "POST" && fw.route.method != method)
        {
            throw new UserException("Use the page action form to make this change.");
        }

        checkXSS();
        checkReadOnly();
    }

    private string bulkAction() => fw.FORM.ContainsKey("delete") ? "delete" : reqs("cms_action");

    private void requireDeletePermission()
    {
        if (fw.userAccessLevel < Users.ACL_SITEADMIN && !fw.model<Users>()
            .isAccessByRolesResourcePermission(fw.userId, "AdminSpages", Permissions.PERMISSION_DELETE))
        {
            throw new AuthException();
        }
    }

    /// <summary>
    /// One-time conversion after installing the CMS schema. Each page commits separately so a failed run can resume.
    /// Applications may remove this action after migration; normal reads never convert or mutate content.
    /// </summary>
    public FwDict MigrateAction()
    {
        enforcePost();
        checkReadOnly();
        model.requireAuthor(true);
        if (fw.userAccessLevel < Users.ACL_SITEADMIN)
        {
            throw new AuthException();
        }

        int count = 0;
        foreach (var row in db.array(model.table_name, []))
        {
            if (row["draft_json"].toStr().Length > 0)
            {
                continue;
            }

            db.begin();
            try
            {
                bool isLeft = row["idesc_left"].toStr().Length > 0;
                bool isRight = row["idesc_right"].toStr().Length > 0;
                string layout = isLeft ? (isRight ? "three-column" : "sidebar-left") : (isRight ? "sidebar-right" : "article");
                string content = SpagesContent.fromMarkdown(row, model.attachmentIdByUrl).ToJsonString();
                var original = new FwDict(row)
                {
                    ["original_template"] = row["template"],
                    ["template"] = layout,
                    ["content_json"] = content
                };
                var item = FormUtils.filter(row, Spages.CONTENT_FIELDS);
                item["template"] = layout;
                item["content_json"] = content;
                string snapshot = Utils.jsonEncode(item);
                DateTime when = DateTime.TryParse(row["pub_time"].toStr(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var publicationTime)
                    ? publicationTime : DateTime.UnixEpoch;
                bool isPublished = row["status"].toInt() == FwModel.STATUS_ACTIVE;
                int status = isPublished ? (when > DateTime.UtcNow ? Spages.STATUS_SCHEDULED : Spages.STATUS_PUBLISHED) : row["status"].toInt();
                model.addRevision(row["id"].toInt(), Spages.KIND_ORIGINAL, Utils.jsonEncode(original), DateTime.UtcNow, "Original content before block conversion");
                model.addRevision(row["id"].toInt(), isPublished ? Spages.KIND_PUBLISHED : Spages.KIND_SAVED, snapshot, when, "Initial block revision");
                db.update(model.table_name, new FwDict
                {
                    ["draft_json"] = snapshot,
                    ["content_json"] = content,
                    ["template"] = layout,
                    ["status"] = status
                }, DB.h("id", row["id"]));
                db.commit();
                count++;
            }
            catch
            {
                db.rollback();
                model.clearCmsCache();
                throw;
            }
        }

        model.clearCmsCache();
        return new FwDict
        {
            ["_json"] = new FwDict
            {
                ["success"] = true,
                ["count"] = count
            }
        };
    }

    public FwDict MediaAction(int id)
    {
        return new FwDict
        {
            ["_json"] = new FwDict { ["items"] = model.listEditorMedia(id) }
        };
    }

    /// <summary>Reuse the standard attachment modal with page-scoped reads and uploads.</summary>
    public FwDict SelectImageAction(int id)
    {
        int categoryId = reqi("att_categories_id");
        if (reqs("category").Length > 0)
        {
            categoryId = fw.model<AttCategories>().oneByIcode(reqs("category"))["id"].toInt();
        }

        return new FwDict
        {
            ["_basedir"] = "/admin/att/select",
            ["upload_url"] = fw.config("ROOT_URL").toStr() + base_url + "/(Upload)/" + id,
            ["file_accept"] = "image/*",
            ["att_dr"] = model.listEditorMedia(id, true, categoryId),
            ["select_att_categories_id"] = fw.model<AttCategories>().listSelectOptions(),
            ["att_categories_id"] = categoryId
        };
    }

    public FwDict UploadAction(int id)
    {
        enforcePost();
        checkReadOnly();
        model.requireAuthor();
        model.oneDraftOrFail(id);
        int categoryId = reqh("item")["att_categories_id"].toInt();
        var rows = fw.model<Att>().uploadMulti(new FwDict
        {
            ["fwentities_id"] = fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_SPAGE),
            ["item_id"] = id,
            ["add_users_id"] = fw.userId,
            ["att_categories_id"] = categoryId > 0 ? categoryId : null
        });
        if (rows.Count == 0)
        {
            throw new UserException("Select a file to upload.");
        }

        var first = rows[0];
        return new FwDict
        {
            ["_json"] = new FwDict
            {
                ["success"] = 1,
                ["id"] = first["id"],
                ["name"] = first["fname"],
                ["iname"] = first["iname"],
                ["url"] = fw.model<Att>().getUrl(first["id"].toInt()),
                ["is_image"] = first["is_image"]
            }
        };
    }

}
