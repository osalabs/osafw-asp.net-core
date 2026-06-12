using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public class AdminKBArticlesController : FwAdminController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected KBArticles model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<KBArticles>();
        model0 = model;

        base_url = "/Admin/KBArticles";
        required_fields = "iname content_markdown";
        save_fields = "icode iname idesc content_markdown access_level status";
        search_fields = "icode iname idesc content_markdown";
        list_sortdef = "upd_time desc";
        list_sortmap = Utils.qh("id|id icode|icode iname|iname access_level|access_level upd_time|upd_time status|status");
    }

    /// <summary>
    /// Allows managers to maintain KB records without requiring a generated RBAC resource.
    /// </summary>
    public override void checkAccess()
    {
        if (fw.userAccessLevel < access_level)
            throw new AuthException("Bad access - Not authorized");
    }

    public override FwDict? IndexAction()
    {
        if (!areTablesReady())
        {
            return new FwDict
            {
                ["title"] = "Knowledge Base",
                ["base_url"] = base_url,
                ["tables_ready"] = false,
            };
        }

        var ps = base.IndexAction() ?? [];
        ps["tables_ready"] = true;
        ps["is_site_admin"] = fw.model<Users>().isSiteAdmin();
        return ps;
    }

    public override void setListSearch()
    {
        base.setListSearch();
        if (!fw.model<Users>().isSiteAdmin())
        {
            list_where += " and access_level<=@current_access_level";
            list_where_params["@current_access_level"] = fw.userAccessLevel;
        }
    }

    public override FwDict ShowAction(int id)
    {
        model.checkAccess(id);
        var ps = base.ShowAction(id) ?? [];
        ps["chunk_count"] = countArticleChunks(id);
        return ps;
    }

    public override FwDict ShowFormAction(int id = 0)
    {
        if (id > 0)
            model.checkAccess(id);

        if (id == 0)
        {
            form_new_defaults = new FwDict
            {
                ["access_level"] = Users.ACL_MEMBER,
                ["status"] = FwModel.STATUS_ACTIVE
            };
        }

        var ps = base.ShowFormAction(id) ?? [];
        ps["is_site_admin"] = fw.model<Users>().isSiteAdmin();
        ps["select_options_access_level"] = accessLevelOptions((ps["i"] as FwDict ?? [])["access_level"].toStr());
        ps["chunk_count"] = id > 0 ? countArticleChunks(id) : 0;
        return ps;
    }

    public override FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM;
        checkReadOnly();

        var item = reqh("item");
        var isNew = id == 0;
        if (string.IsNullOrWhiteSpace(item["icode"].toStr()))
            item["icode"] = buildArticleCode(item["iname"].toStr(), id);

        Validate(id, item);

        var itemdb = FormUtils.filter(item, save_fields);
        itemdb["access_level"] = itemdb["access_level"].toInt(Users.ACL_MEMBER);
        itemdb["status"] = itemdb["status"].toInt(FwModel.STATUS_ACTIVE);
        id = modelAddOrUpdate(id, itemdb);

        bool indexed = model.reindexKBArticle(id);
        fw.flash(indexed ? "success" : "info", indexed ? "Knowledge base article indexed." : "Knowledge base article saved. Indexing is pending setup or provider availability.");

        return afterSave(true, id, isNew);
    }

    public override void Validate(int id, FwDict item)
    {
        bool result = validateRequired(id, item, required_fields);
        item["icode"] = buildArticleCode(item["icode"].toStr(), id);

        if (result && model.isExistsByField(item["icode"].toStr(), id, "icode"))
            fw.FormErrors["icode"] = "EXISTS";

        if (!fw.model<Users>().isSiteAdmin() && item["access_level"].toInt(Users.ACL_MEMBER) > fw.userAccessLevel)
            fw.FormErrors["access_level"] = "ACCESS";

        validateCheckResult();
    }

    public FwDict? ReindexAction(int id)
    {
        enforcePost();
        model.checkAccess(id);
        bool indexed = model.reindexKBArticle(id);
        fw.flash(indexed ? "success" : "error", indexed ? "Knowledge base article reindexed." : "Knowledge base article could not be indexed. Check assistant setup and logs.");
        fw.redirect(base_url + "/" + id);
        return null;
    }

    private int countArticleChunks(int id)
    {
        if (!areTablesReady())
            return 0;

        try
        {
            int entityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_KB);
            if (entityId <= 0)
                return 0;

            return db.valuep("select count(*) from doc_chunks where fwentities_id=@fwentities_id and item_id=@item_id and status<>@status_deleted", DB.h(
                "@fwentities_id", entityId,
                "@item_id", id,
                "@status_deleted", FwModel.STATUS_DELETED
            )).toInt();
        }
        catch
        {
            return 0;
        }
    }

    private string accessLevelOptions(string selected)
    {
        var rows = new FwList
        {
            DB.h("id", Users.ACL_MEMBER, "iname", "Member"),
            DB.h("id", Users.ACL_MANAGER, "iname", "Manager"),
            DB.h("id", Users.ACL_ADMIN, "iname", "Administrator"),
            DB.h("id", Users.ACL_SITEADMIN, "iname", "Site Administrator")
        };
        return FormUtils.selectOptions(rows, selected);
    }

    private static string buildArticleCode(string value, int id)
    {
        string code = Regex.Replace(value ?? string.Empty, @"^\W+", "");
        code = Regex.Replace(code, @"\W+$", "");
        code = Regex.Replace(code, @"\W+", "-").Trim('-').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code))
            code = id > 0 ? "kb-" + id : "kb-" + Utils.uuid();
        return code.Length > 80 ? code[..80] : code;
    }

    private bool areTablesReady()
    {
        try
        {
            var tables = db.tables().Select(static table => table.ToString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return tables.Contains("kb_articles") && tables.Contains("doc_chunks");
        }
        catch
        {
            return false;
        }
    }
}
