using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public class AdminKBArticlesController : FwDynamicController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected KBArticles model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Admin/KBArticles";
        loadControllerConfig();
        model = model0 as KBArticles ?? throw new FwConfigUndefinedModelException();
        db = model.getDB();
    }

    /// <summary>
    /// Allows managers to maintain KB records without requiring a generated RBAC resource.
    /// </summary>
    public override void checkAccess()
    {
        if (fw.userAccessLevel < access_level)
            throw new AuthException("Bad access - Not authorized");
    }

    public override FwDict IndexAction()
    {
        if (!areTablesReady())
        {
            return new FwDict
            {
                ["title"] = "Knowledge Base",
                ["base_url"] = base_url,
                ["count"] = 0,
                ["f"] = reqh("f"),
                ["tables_ready"] = false,
                ["is_site_admin"] = fw.model<Users>().isSiteAdmin(),
            };
        }

        var ps = base.IndexAction();
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
        if (!areTablesReady())
            throw new UserException("Knowledge base tables are not installed.");

        model.checkAccess(id);
        var ps = base.ShowAction(id) ?? [];
        ps["chunk_count"] = countArticleChunks(id);
        return ps;
    }

    public override FwDict ShowFormAction(int id = 0)
    {
        if (!areTablesReady())
            throw new UserException("Knowledge base tables are not installed.");

        if (id > 0)
            model.checkAccess(id);

        var ps = base.ShowFormAction(id) ?? [];
        ps["is_site_admin"] = fw.model<Users>().isSiteAdmin();
        ps["chunk_count"] = id > 0 ? countArticleChunks(id) : 0;
        return ps;
    }

    public override FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM;
        checkReadOnly();

        if (!areTablesReady())
            throw new UserException("Knowledge base tables are not installed.");

        if (id > 0)
            model.checkAccess(id);

        if (reqb("refresh"))
        {
            fw.routeRedirect(FW.ACTION_SHOW_FORM, [id]);
            return null;
        }

        var item = reqh("item");
        var itemOld = id > 0 ? model.one(id) : [];
        var isNew = id == 0;
        if (string.IsNullOrWhiteSpace(item["icode"].toStr()))
            item["icode"] = itemOld.Count > 0 && string.IsNullOrWhiteSpace(item["iname"].toStr())
                ? itemOld["icode"].toStr()
                : buildArticleCode(item["iname"].toStr(), id);

        Validate(id, item);

        var itemdb = FormUtils.filter(item, save_fields);
        itemdb["access_level"] = itemdb.ContainsKey("access_level")
            ? itemdb["access_level"].toInt(Users.ACL_MEMBER)
            : itemOld["access_level"].toInt(Users.ACL_MEMBER);
        itemdb["status"] = itemdb.ContainsKey("status")
            ? itemdb["status"].toInt(FwModel.STATUS_ACTIVE)
            : itemOld["status"].toInt(FwModel.STATUS_ACTIVE);
        id = modelAddOrUpdate(id, itemdb);

        bool queued = model.reindexKBArticle(id);
        fw.flash(queued ? "success" : "info", queued ? "Knowledge base article queued for indexing." : "Knowledge base article saved. Indexing is pending assistant setup or provider availability.");

        return afterSave(true, id, isNew);
    }

    public override string applyViewListConversions(string fieldname, FwDict row, FwDict hconversions)
    {
        if (fieldname == "access_level")
            return FormUtils.selectTplName("/admin/kbarticles/access_level.sel", row[fieldname].toStr());

        if (fieldname == "status")
            return FormUtils.selectTplName("/common/sel/status.sel", row[fieldname].toStr());

        return base.applyViewListConversions(fieldname, row, hconversions);
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
        bool queued = model.reindexKBArticle(id);
        fw.flash(queued ? "success" : "error", queued ? "Knowledge base article queued for reindexing." : "Knowledge base article could not be queued. Check assistant setup and logs.");
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

            return db.valuep("select count(*) from rag_chunks where fwentities_id=@fwentities_id and item_id=@item_id and status<>@status_deleted", DB.h(
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
            return tables.Contains("kb_articles") && tables.Contains("rag_sources") && tables.Contains("rag_chunks");
        }
        catch
        {
            return false;
        }
    }
}
