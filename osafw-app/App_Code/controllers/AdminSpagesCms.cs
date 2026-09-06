using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace osafw;

public partial class AdminSpagesController
{
    public override void checkAccess()
    {
        if (!model.isEnabled() || fw.userAccessLevel < model.authorLevel()) throw new AuthException();
        if (model.isCmsReady() && fw.route.action is "SaveMulti" or "SaveSort" or "RestoreDeleted")
            throw new UserException("Use the CMS draft and publication controls to change pages.");
        // Existing RBAC remains an additional restriction; workflow roles never grant a missing resource permission.
        access_actions_to_permissions = new() {
            { "Publish", Permissions.PERMISSION_EDIT }, { "Submit", Permissions.PERMISSION_EDIT },
            { "Changes", Permissions.PERMISSION_EDIT }, { "Unpublish", Permissions.PERMISSION_EDIT },
            { "Cancel", Permissions.PERMISSION_EDIT }, { "RestoreRevision", Permissions.PERMISSION_EDIT },
            { "Upload", Permissions.PERMISSION_EDIT }, { "Preview", Permissions.PERMISSION_VIEW },
            { "Upgrade", Permissions.PERMISSION_EDIT }, { "InstallSamples", Permissions.PERMISSION_EDIT },
            { "Redirects", Permissions.PERMISSION_LIST }, { "SaveRedirect", Permissions.PERMISSION_EDIT },
            { "RemoveRedirect", Permissions.PERMISSION_EDIT }, { "Media", Permissions.PERMISSION_VIEW }
        };
        base.checkAccess();
    }

    public override FwDict? IndexAction()
    {
        if (!model.isCmsReady()) return base.IndexAction();
        string search = reqs("s").Trim();
        string state = reqs("state");
        string kind = reqs("kind");
        var rows = new FwList();
        int unmigrated = db.valuep("SELECT COUNT(*) FROM " + db.qid(model.table_name) + " WHERE draft_json IS NULL OR draft_json='' ").toInt();
        foreach (var raw in db.array(model.table_name, DB.h("status", db.opNOT(FwModel.STATUS_DELETED)), "iname"))
        {
            var item = model.draft(raw["id"].toInt());
            if (search.Length > 0 && !(item["iname"].toStr() + " " + item["url"].toStr() + " " + item["snippet_key"].toStr()).Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            if (state.Length > 0 && item["workflow"].toStr() != state) continue;
            if (kind == "snippets" && !item["is_snippet"].toBool() || kind == "pages" && item["is_snippet"].toBool()) continue;
            item["full_url"] = model.publishedUrl(raw["id"].toInt());
            item["is_live"] = !item["is_snippet"].toBool() && model.published(raw["id"].toInt(), 100).Count > 0;
            rows.Add(item);
        }
        const int pageSize = 30;
        int pagenum = Math.Max(0, reqi("pagenum"));
        string filterUrl = "?s=" + Uri.EscapeDataString(search) + "&state=" + Uri.EscapeDataString(state) + "&kind=" + Uri.EscapeDataString(kind) + "&pagenum=";
        return DB.h("cms", true, "list_rows", new FwList(rows.Skip(pagenum * pageSize).Take(pageSize)), "count", rows.Count, "prev_url", pagenum > 0 ? filterUrl + (pagenum - 1) : "", "next_url", (pagenum + 1) * pageSize < rows.Count ? filterUrl + (pagenum + 1) : "", "s", search, "state", state, "kind", kind, "needs_upgrade", unmigrated > 0, "is_site_admin", fw.userAccessLevel >= Users.ACL_SITEADMIN, "can_publish", model.canPublish());
    }

    private FwDict cmsForm(int id)
    {
        var item = id > 0 ? model.draft(id) : DB.h("iname", "", "parent_id", reqi("parent_id"), "is_snippet", reqi("snippet"), "template", "article", "content_json", SpagesContent.empty().ToJsonString(), "nav_visible", 1, "edit_version", 0, "workflow", "draft");
        var parents = new FwList();
        var parentOptions = new FwList();
        var snippets = new FwList();
        var all = db.array(model.table_name, DB.h("status", db.opNOT(FwModel.STATUS_DELETED)), "iname");
        foreach (var raw in all)
        {
            var other = model.draft(raw["id"].toInt());
            if (other["is_snippet"].toBool()) snippets.Add(DB.h("key", other["snippet_key"], "title", other["iname"]));
            else if (other["id"].toInt() != id) parentOptions.Add(other);
        }
        int parentId = item["parent_id"].toInt();
        var seen = new System.Collections.Generic.HashSet<int> { id };
        while (parentId > 0 && seen.Add(parentId) && seen.Count <= 20)
        {
            var parent = model.draft(parentId);
            parents.Insert(0, parent);
            parentId = parent["parent_id"].toInt();
        }
        var layouts = new FwList();
        foreach (var entry in SpagesContent.layouts())
            if (!item["is_snippet"].toBool() || entry.Key == "article") layouts.Add(DB.h("id", entry.Key, "title", entry.Value.Title));
        FwDict ps = DB.h("cms", true, "id", id, "i", item, "parents", parents, "parent_options", parentOptions, "layouts", layouts, "layouts_json", Utils.jsonEncode(SpagesContent.layouts().ToDictionary(x => x.Key, x => new { regions = x.Value.Regions, slots = x.Value.Slots })), "snippets_json", Utils.jsonEncode(snippets), "history", id > 0 ? model.history(id) : new FwList(), "can_publish", model.canPublish(), "is_readonly", !model.canAuthor(), "is_showform", true, "full_url", id > 0 ? model.publishedUrl(id) : "", "usages", item["is_snippet"].toBool() ? model.usages(item["snippet_key"].toStr(), true) : new FwList());
        ps["is_site_admin"] = fw.model<Users>().isAccessLevel(Users.ACL_SITEADMIN);
        ps["has_scheduled"] = id > 0 && db.value(Spages.RevisionTable, DB.h("spages_id", id, "kind", "published", "cancelled", 0, "effective_time", db.opGT(DateTime.UtcNow)), "id").toInt() > 0;
        ps["is_live"] = id > 0 && model.published(id, 100).Count > 0;
        ps["is_in_review"] = item["workflow"].toStr() == "in_review";
        return ps;
    }

    private FwDict cmsSave(int id)
    {
        enforcePost();
        checkReadOnly();
        try
        {
            int savedId = model.saveDraft(id, reqh("item"), reqi("expected_version"), reqb("autosave"));
            return DB.h("_json", DB.h("success", true, "id", savedId, "version", model.draft(savedId)["edit_version"], "location", base_url + "/" + savedId + "/edit"));
        }
        catch (SpagesConflictException ex) { return conflict(ex); }
    }

    private FwDict workflow(int id, string action)
    {
        enforcePost();
        checkReadOnly();
        DateTime? publishAt = null;
        string date = reqs("publish_at");
        if (action == "publish" && date.Length > 0)
        {
            if (!DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)) throw new UserException("Enter a valid publication date and time.");
            publishAt = parsed.UtcDateTime;
        }
        try
        {
            int revision = model.transition(id, reqi("expected_version"), action, reqs("note"), publishAt);
            return DB.h("_json", DB.h("success", true, "revision_id", revision, "version", model.draft(id)["edit_version"], "workflow", model.draft(id)["workflow"]));
        }
        catch (SpagesConflictException ex) { return conflict(ex); }
    }

    private FwDict conflict(SpagesConflictException ex)
    {
        fw.response.StatusCode = 409;
        return DB.h("_json", DB.h("success", false, "error", DB.h("message", ex.Message), "conflict", true));
    }

    public FwDict PublishAction(int id) => workflow(id, "publish");
    public FwDict SubmitAction(int id) => workflow(id, "submit");
    public FwDict ChangesAction(int id) => workflow(id, "changes");
    public FwDict UnpublishAction(int id) => workflow(id, "unpublish");
    public FwDict CancelAction(int id) => workflow(id, "cancel");

    public FwDict RestoreRevisionAction(int id)
    {
        enforcePost(); checkReadOnly();
        try
        {
            model.restoreRevision(id, reqi("revision_id"), reqi("expected_version"));
            return DB.h("_json", DB.h("success", true, "version", model.draft(id)["edit_version"]));
        }
        catch (SpagesConflictException ex) { return conflict(ex); }
    }

    public void PreviewAction(int id)
    {
        model.requireAuthor();
        if (!model.canPreview()) throw new AuthException();
        int revisionId = reqi("revision_id");
        var item = revisionId > 0 ? model.revision(id, revisionId) : model.draft(id);
        fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_PUBLIC");
        fw.cache_control = "private, no-store";
        fw.response.Headers.CacheControl = fw.cache_control;
        fw.response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        fw.parser("/home/spage", model.pageState(item, true, revisionId > 0));
    }

    public override FwDict? DeleteAction(int id)
    {
        if (!model.isCmsReady()) return base.DeleteAction(id);
        enforcePost(); checkReadOnly(); model.requireAuthor(true);
        var item = model.draft(id);
        model.transition(id, reqi("expected_version"), "unpublish", "Page withdrawn");
        return DB.h("_json", DB.h("success", true));
    }

    public FwDict UpgradeAction()
    {
        enforcePost(); checkReadOnly();
        int count = model.migrateLegacy();
        return DB.h("_json", DB.h("success", true, "count", count));
    }

    public FwDict MediaAction(int id)
    {
        model.requireAuthor(); model.draft(id);
        int entity = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_SPAGE);
        var rows = db.arrayp("SELECT id,iname,fname,icode,is_image FROM att WHERE status=0 AND ((fwentities_id=@entity AND item_id=@id) OR fwentities_id IS NULL OR fwentities_id=0) ORDER BY id DESC", DB.h("entity", entity, "id", id));
        return DB.h("_json", DB.h("items", new FwList(rows.Take(100).Select(x => new FwDict(x)))));
    }

    public FwDict UploadAction(int id)
    {
        enforcePost(); checkReadOnly(); model.requireAuthor(); model.draft(id);
        var rows = fw.model<Att>().uploadMulti(DB.h("fwentities_id", fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_SPAGE), "item_id", id, "add_users_id", fw.userId));
        if (rows.Count == 0) throw new UserException("Select a file to upload.");
        var first = rows[0];
        return DB.h("_json", DB.h("success", 1, "id", first["id"], "name", first["fname"]));
    }

    public FwDict RedirectsAction()
    {
        model.requireAuthor(true);
        return DB.h("items", db.array(Spages.RedirectTable, DB.h("status", 0, "revision_id", 0), "source_url"));
    }

    public FwDict SaveRedirectAction()
    {
        enforcePost(); checkReadOnly();
        return DB.h("_json", DB.h("success", true, "id", model.saveRedirect(reqs("source_url"), reqs("target_url"))));
    }

    public FwDict RemoveRedirectAction(int id)
    {
        enforcePost(); checkReadOnly(); model.removeRedirect(id);
        return DB.h("_json", DB.h("success", true));
    }

    public FwDict InstallSamplesAction()
    {
        enforcePost(); checkReadOnly(); model.requireAuthor(true);
        if (fw.userAccessLevel < Users.ACL_SITEADMIN) throw new AuthException();
        return DB.h("_json", DB.h("success", true, "ids", model.installSamples(reqs("sample"))));
    }
}
