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

public class AdminSpagesController : FwAdminController
{
    public static new int access_level = Users.ACL_MEMBER;
    protected Spages model = null!;
    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<Spages>();
        model0 = model;
        // initialization
        base_url = "/Admin/Spages";
        required_fields = "iname";
        save_fields = "iname idesc idesc_left idesc_right head_att_id template prio meta_keywords meta_description custom_head custom_css custom_js redirect_url";
        search_fields = "url iname idesc";
        list_sortdef = "iname asc"; // default sorting: name, asc|desc direction
        list_sortmap = Utils.qh("id|id iname|iname pub_time|pub_time upd_time|upd_time status|status url|url");
    }

    public override void getListRows()
    {
        if (list_filter["sortby"].toStr() == "iname" && list_filter["s"].toStr() == "" && (this.list_filter["status"].toStr() == "" || this.list_filter["status"].toStr() == "0"))
        {
            // show tree only if sort by title and no search and status by all or active
            this.list_count = db.valuep("select count(*) from " + db.qid(model.table_name) + " where " + this.list_where, this.list_where_params).toLong();
            if (this.list_count > 0)
            {
                // build pages tree
                FwList pages_tree = model.listTreeByFilter(this.list_where, this.list_where_params, "parent_id, prio desc, iname");
                this.list_rows = model.listTreeFlat(pages_tree, 0);
                // apply LIMIT
                var pagesize = this.list_filter["pagesize"].toInt();
                var pagenum = this.list_filter["pagenum"].toInt();
                if (this.list_count > pagesize)
                {
                    FwList subset = [];
                    int start_offset = pagenum * pagesize;
                    for (int i = start_offset; i <= Math.Min(start_offset + pagesize, this.list_rows.Count) - 1; i++)
                        subset.Add(this.list_rows[i]);
                    this.list_rows = subset;
                }

                this.list_pager = FormUtils.getPager(this.list_count, pagenum, pagesize);
            }
            else
            {
                this.list_rows = [];
                this.list_pager = [];
            }
        }
        else
            // if order not by iname or search performed - display plain page list using  Me.get_list_rows()
            base.getListRows();
        // add/modify rows from db if necessary
        foreach (FwDict row in this.list_rows)
        {
            row["full_url"] = model.getFullUrl(row["id"].toInt());
        }
    }

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

        if (fw.route.action is "SaveMulti" or "SaveSort" or "RestoreDeleted")
        {
            throw new UserException("Use the CMS draft and publication controls to change pages.");
        }

        // Existing RBAC remains an additional restriction; workflow roles never grant a missing resource permission.
        access_actions_to_permissions = new()
        {
            ["Publish"] = Permissions.PERMISSION_EDIT,
            ["Submit"] = Permissions.PERMISSION_EDIT,
            ["Changes"] = Permissions.PERMISSION_EDIT,
            ["Unpublish"] = Permissions.PERMISSION_EDIT,
            ["Cancel"] = Permissions.PERMISSION_EDIT,
            ["RestoreRevision"] = Permissions.PERMISSION_EDIT,
            ["Upload"] = Permissions.PERMISSION_EDIT,
            ["Preview"] = Permissions.PERMISSION_VIEW,
            ["Migrate"] = Permissions.PERMISSION_EDIT,
            ["Media"] = Permissions.PERMISSION_VIEW
        };
        base.checkAccess();
    }

    public override FwDict? IndexAction()
    {
        string search = reqs("s").Trim();
        string state = reqs("state");
        string kind = reqs("kind");
        var rows = new FwList();
        int unmigrated = db.valuep("SELECT COUNT(*) FROM " + db.qid(model.table_name) + " WHERE draft_json IS NULL OR draft_json='' ").toInt();
        foreach (var raw in db.array(model.table_name, DB.h("status", db.opNOT(FwModel.STATUS_DELETED)), "iname"))
        {
            if (raw["draft_json"].toStr().Length == 0)
            {
                continue;
            }

            var item = model.oneDraftOrFail(raw["id"].toInt());
            if (search.Length > 0 && !(item["iname"].toStr() + " " + item["url"].toStr()).Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (state.Length > 0 && item["workflow"].toStr() != state)
            {
                continue;
            }

            if (kind == "snippets" && !item["is_snippet"].toBool() || kind == "pages" && item["is_snippet"].toBool())
            {
                continue;
            }

            item["full_url"] = model.publishedUrl(raw["id"].toInt());
            item["is_live"] = !item["is_snippet"].toBool() && model.onePublished(raw["id"].toInt(), 100).Count > 0;
            item["workflow_label"] = Spages.workflowLabel(item["workflow"].toInt());
            rows.Add(item);
        }

        const int PAGE_SIZE = 30;
        int pagenum = Math.Max(0, reqi("pagenum"));
        string filterUrl = "?s=" + Uri.EscapeDataString(search) + "&state=" + Uri.EscapeDataString(state) + "&kind=" + Uri.EscapeDataString(kind) + "&pagenum=";
        return new FwDict
        {
            ["cms"] = true,
            ["list_rows"] = new FwList(rows.Skip(pagenum * PAGE_SIZE).Take(PAGE_SIZE)),
            ["count"] = rows.Count,
            ["prev_url"] = pagenum > 0 ? filterUrl + (pagenum - 1) : "",
            ["next_url"] = (pagenum + 1) * PAGE_SIZE < rows.Count ? filterUrl + (pagenum + 1) : "",
            ["s"] = search,
            ["state"] = state,
            ["kind"] = kind,
            ["is_needs_migration"] = unmigrated > 0,
            ["is_site_admin"] = fw.userAccessLevel >= Users.ACL_SITEADMIN,
            ["is_publisher"] = model.isPublisher()
        };
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
            ["workflow"] = Spages.WORKFLOW_DRAFT
        };
        item["workflow_label"] = Spages.workflowLabel(item["workflow"].toInt());
        var parents = new FwList();
        var parentOptions = new FwList();
        var snippets = new FwList();
        var all = db.array(model.table_name, DB.h("status", db.opNOT(FwModel.STATUS_DELETED)), "iname");
        foreach (var raw in all)
        {
            if (raw["draft_json"].toStr().Length == 0)
            {
                continue;
            }

            var other = model.oneDraftOrFail(raw["id"].toInt());
            if (other["is_snippet"].toBool())
            {
                snippets.Add(new FwDict { ["key"] = other["url"], ["title"] = other["iname"] });
            }
            else if (other["id"].toInt() != id)
            {
                parentOptions.Add(other);
            }
        }

        int parentId = item["parent_id"].toInt();
        var seen = new System.Collections.Generic.HashSet<int>
        {
            id
        };
        while (parentId > 0 && seen.Add(parentId) && seen.Count <= 20)
        {
            var parent = model.oneDraftOrFail(parentId);
            parents.Insert(0, parent);
            parentId = parent["parent_id"].toInt();
        }

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
            ["parents"] = parents,
            ["parent_options"] = parentOptions,
            ["layouts"] = layouts,
            ["layouts_json"] = Utils.jsonEncode(SpagesContent.layouts().ToDictionary(x => x.Key, x => new { regions = x.Value.Regions, slots = x.Value.Slots })),
            ["snippets_json"] = Utils.jsonEncode(snippets),
            ["history"] = id > 0 ? model.listRevisions(id) : new FwList(),
            ["is_publisher"] = model.isPublisher(),
            ["is_readonly"] = !model.isAuthor(),
            ["is_showform"] = true,
            ["full_url"] = id > 0 ? model.publishedUrl(id) : "",
            ["usages"] = item["is_snippet"].toBool() ? model.listSnippetUsages(item["url"].toStr(), true) : new FwList()
        };
        ps["is_site_admin"] = fw.model<Users>().isAccessLevel(Users.ACL_SITEADMIN);
        ps["is_author"] = model.isAuthor();
        ps["is_scheduled"] = id > 0 && model.isScheduled(id);
        ps["is_live"] = id > 0 && model.onePublished(id, 100).Count > 0;
        ps["is_in_review"] = item["workflow"].toInt() == Spages.WORKFLOW_IN_REVIEW;
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
                ["workflow"] = item["workflow"],
                ["workflow_label"] = Spages.workflowLabel(item["workflow"].toInt())
            }
        };
    }

    public FwDict PublishAction(int id) => workflow(id, "publish");

    public FwDict SubmitAction(int id) => workflow(id, "submit");

    public FwDict ChangesAction(int id) => workflow(id, "changes");

    public FwDict UnpublishAction(int id) => workflow(id, "unpublish");

    public FwDict CancelAction(int id) => workflow(id, "cancel");

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
        model.requireAuthor();
        if (!model.isPreviewAllowed())
        {
            throw new AuthException();
        }

        int revisionId = reqi("revision_id");
        var item = revisionId > 0 ? model.oneRevisionOrFail(id, revisionId) : model.oneDraftOrFail(id);
        fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_PUBLIC");
        fw.cache_control = "private, no-store";
        fw.response.Headers.CacheControl = fw.cache_control;
        fw.response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        fw.parser("/home/spage", model.buildPageState(item, true, revisionId > 0));
    }

    public override FwDict? DeleteAction(int id)
    {
        enforcePost();
        checkReadOnly();
        model.requireAuthor(true);
        model.updateWorkflow(id, "unpublish", "Page withdrawn");
        return new FwDict
        {
            ["_json"] = new FwDict
            {
                ["success"] = true
            }
        };
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
                int workflow = isPublished ? (when > DateTime.UtcNow ? Spages.WORKFLOW_SCHEDULED : Spages.WORKFLOW_PUBLISHED) : Spages.WORKFLOW_DRAFT;
                model.addRevision(row["id"].toInt(), Spages.KIND_ORIGINAL, Utils.jsonEncode(original), DateTime.UtcNow, "Original content before block conversion");
                model.addRevision(row["id"].toInt(), isPublished ? Spages.KIND_PUBLISHED : Spages.KIND_SAVED, snapshot, when, "Initial block revision");
                db.update(model.table_name, new FwDict
                {
                    ["draft_json"] = snapshot,
                    ["content_json"] = content,
                    ["template"] = layout,
                    ["workflow"] = workflow
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
        model.requireAuthor();
        model.oneDraftOrFail(id);
        int entity = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_SPAGE);
        var rows = db.arrayp("SELECT id,iname,fname,icode,is_image FROM att WHERE status=0 AND ((fwentities_id=@entity AND item_id=@id) OR fwentities_id IS NULL OR fwentities_id=0) ORDER BY id DESC", DB.h("entity", entity, "id", id));
        return new FwDict
        {
            ["_json"] = new FwDict
            {
                ["items"] = new FwList(rows.Take(100).Select(x => new FwDict(x)))
            }
        };
    }

    public FwDict UploadAction(int id)
    {
        enforcePost();
        checkReadOnly();
        model.requireAuthor();
        model.oneDraftOrFail(id);
        var rows = fw.model<Att>().uploadMulti(new FwDict
        {
            ["fwentities_id"] = fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_SPAGE),
            ["item_id"] = id,
            ["add_users_id"] = fw.userId
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
                ["name"] = first["fname"]
            }
        };
    }

}
