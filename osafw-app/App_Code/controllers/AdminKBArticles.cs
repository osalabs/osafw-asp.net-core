using System;
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
        is_userlists = true;
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
        if (!model.isTablesReady())
        {
            return new FwDict
            {
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
        if (!model.isTablesReady())
            throw new UserException("Knowledge base tables are not installed.");

        model.checkAccess(id);
        var ps = base.ShowAction(id) ?? [];
        ps["chunk_count"] = model.countChunks(id);
        prepareKbAttachmentFields(ps, id);
        return ps;
    }

    public override FwDict ShowFormAction(int id = 0)
    {
        if (!model.isTablesReady())
            throw new UserException("Knowledge base tables are not installed.");

        if (id > 0)
            model.checkAccess(id);

        var ps = base.ShowFormAction(id) ?? [];
        ps["is_site_admin"] = fw.model<Users>().isSiteAdmin();
        ps["chunk_count"] = id > 0 ? model.countChunks(id) : 0;
        prepareKbAttachmentFields(ps, id);
        return ps;
    }

    public override FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM;
        checkReadOnly();

        if (!model.isTablesReady())
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
        syncKbFilesFromRequest(id);

        bool isQueued = model.queueReindex(id);
        fw.flash(isQueued ? "success" : "info", isQueued ? "Knowledge base article queued for indexing." : "Knowledge base article saved. Indexing is pending assistant setup or provider availability.");

        return afterSave(true, id, isNew);
    }

    public override void Validate(int id, FwDict item)
    {
        bool isValid = validateRequired(id, item, required_fields);
        item["icode"] = buildArticleCode(item["icode"].toStr(), id);

        if (isValid && model.isExistsByField(item["icode"].toStr(), id, "icode"))
            fw.FormErrors["icode"] = "EXISTS";

        if (!fw.model<Users>().isSiteAdmin() && item["access_level"].toInt(Users.ACL_MEMBER) > fw.userAccessLevel)
            fw.FormErrors["access_level"] = "ACCESS";

        validateCheckResult();
    }

    public FwDict? ReindexAction(int id)
    {
        enforcePost();
        if (!model.isTablesReady())
            throw new UserException("Knowledge base tables are not installed.");

        model.checkAccess(id);
        bool isQueued = model.queueReindex(id);
        fw.flash(isQueued ? "success" : "error", isQueued ? "Knowledge base article queued for reindexing." : "Knowledge base article could not be queued. Check assistant setup and logs.");
        fw.redirect(base_url + "/" + id);
        return null;
    }

    /// <summary>
    /// Uploads KB article files under the KB entity code so RAG attachment indexing sees the same records.
    /// </summary>
    public override FwDict SaveAttFilesAction(int id)
    {
        enforcePost();
        checkReadOnly();
        if (!model.isTablesReady())
            throw new UserException("Knowledge base tables are not installed.");

        model.checkAccess(id);

        var item = reqh("item");
        var files = fw.request?.Form?.Files;
        if (files == null || files.Count == 0 || files[0] == null || files[0].Length == 0)
            throw new UserException("No file(s) selected");

        var modelAtt = fw.model<Att>();
        var attCategory = fw.model<AttCategories>().oneByIcode(item["att_category"].toStr(AttCategories.CAT_GENERAL));
        var itemdb = new FwDict
        {
            ["item_id"] = id,
            ["att_categories_id"] = attCategory.Count > 0 ? attCategory["id"].toInt() : null,
            ["fwentities_id"] = fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_KB),
            ["status"] = FwModel.STATUS_ACTIVE
        };

        var addedAtt = modelAtt.uploadMulti(itemdb);
        if (addedAtt.Count > 0)
            model.queueReindex(id);

        var response = new FwDict();
        var json = new FwDict();
        int attId = addedAtt.Count > 0 ? (addedAtt[0] as FwDict)!["id"].toInt() : 0;
        json["id"] = attId;
        if (attId > 0)
        {
            var itemNew = modelAtt.one(attId);
            json["icode"] = itemNew["icode"];
            json["url"] = modelAtt.getUrl(attId);
            json["url_preview"] = modelAtt.getUrlPreview(attId);
            json["iname"] = itemNew["iname"];
            json["is_image"] = itemNew["is_image"];
            json["fsize"] = itemNew["fsize"];
            json["ext"] = itemNew["ext"];
        }
        else
        {
            json["error"] = new FwDict { ["message"] = "File upload error" };
        }

        response["_json"] = json;
        return response;
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

    private void prepareKbAttachmentFields(FwDict ps, int id)
    {
        if (id <= 0 || ps["fields"] is not FwList fields)
            return;

        foreach (var item in fields)
        {
            if (item is not FwDict def)
                continue;

            string type = def["type"].toStr();
            if (type != "att_files" && type != "att_files_edit")
                continue;

            if (def["field"].toStr() != "kb_files")
                continue;

            def["att_category"] = def["att_category"].toStr(AttCategories.CAT_GENERAL);
            def["att_files"] = fw.model<Att>().listByEntityCategory(FwEntities.ICODE_KB, id, def["att_category"].toStr());
            if (type == "att_files_edit")
            {
                def["att_upload_url"] = base_url + "/(SaveAttFiles)/" + id;
                def["att_post_prefix"] = "kb_files";
                def["fwentity"] = FwEntities.ICODE_KB;
            }
        }
    }

    private void syncKbFilesFromRequest(int id)
    {
        if (id <= 0 || req("kb_files") == null)
            return;

        var postedIds = reqh("kb_files");
        var existing = fw.model<Att>().listByEntityCategory(FwEntities.ICODE_KB, id, AttCategories.CAT_GENERAL);
        foreach (FwDict row in existing)
        {
            string rowId = row["id"].toStr();
            if (rowId.Length > 0 && !postedIds.ContainsKey(rowId))
                fw.model<Att>().delete(rowId.toInt(), true);
        }
    }
}
