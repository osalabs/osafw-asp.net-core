// Admin Att controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class AdminAttController : FwAdminController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected Att model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<Att>();
        model0 = model;

        base_url = "/Admin/Att"; // base url for the controller
        required_fields = "iname"; // default required fields, space-separated
        save_fields = "fwentities_id item_id att_categories_id iname status";

        search_fields = "!id iname fname";
        list_sortdef = "iname asc";
        list_sortmap = Utils.qh("id|id iname|iname add_time|add_time fsize|fsize ext|ext category|att_categories_id status|status");
    }

    public override void checkAccess()
    {
        // add custom actions to permissions mapping
        access_actions_to_permissions = new() {
            { "Select", Permissions.PERMISSION_LIST },
        };
        base.checkAccess();
    }

    public override void setListSearch()
    {
        list_where = " fwentities_id IS NULL "; //only show uploads directly from user (not linked to specific entity)

        base.setListSearch();

        if (!Utils.isEmpty(list_filter["att_categories_id"]))
        {
            list_where += " and att_categories_id=@att_categories_id";
            list_where_params["@att_categories_id"] = list_filter["att_categories_id"].toInt();
        }
    }

    public override void getListRows()
    {
        base.getListRows();
        foreach (Hashtable row in this.list_rows)
        {
            if (row["is_image"].toInt() == 1)
            {
                row["url_s"] = model.getUrl(row["id"].toInt(), "s");
            }

            var att_categories_id = row["att_categories_id"].toInt();
            if (att_categories_id > 0)
                row["cat"] = fw.model<AttCategories>().one(att_categories_id);
        }
    }

    public override Hashtable IndexAction()
    {
        var ps = base.IndexAction() ?? [];

        ps["select_att_categories_ids"] = fw.model<AttCategories>().listSelectOptions();
        return ps;
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        var ps = base.ShowFormAction(id) ?? [];
        var item = ps["i"] as Hashtable ?? [];

        ps["url"] = model.getUrl(id);
        if (item["is_image"].toInt() == 1)
            ps["url_m"] = model.getUrl(id, "m");

        ps["select_options_att_categories_id"] = fw.model<AttCategories>().listSelectOptions();

        return ps;
    }

    public override Hashtable? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        Hashtable ps = [];
        Hashtable item = reqh("item");
        var is_new = (id == 0);
        var location = "";

        Validate(id, item);
        // load old record if necessary
        // var itemOld = model.one(id);

        // set att_categories_id from category icode if provided
        if (Utils.isEmpty(item["att_categories_id"]) && !Utils.isEmpty(item["att_category"]))
        {
            var att_cat = fw.model<AttCategories>().oneByIcode(item["att_category"].toStr());
            var att_categories_id = att_cat.Count > 0 ? att_cat["id"].toInt() : 0;
            item["att_categories_id"] = att_categories_id;
        }

        // set fwentities_id from fwentity if provided
        if (Utils.isEmpty(item["fwentities_id"]) && !Utils.isEmpty(item["fwentity"]))
        {
            var ent = fw.model<FwEntities>().oneByIcode(item["fwentity"].toStr());
            var fwentities_id = ent.Count > 0 ? ent["id"].toInt() : 0;
            item["fwentities_id"] = fwentities_id;
        }

        Hashtable itemdb = FormUtils.filter(item, save_fields);
        if (Utils.isEmpty(itemdb["iname"]))
            itemdb["iname"] = "new file upload";

        if (id > 0)
        {
            model.update(id, itemdb);
            fw.flash("updated", 1);

            // Proceed upload, if any - for edit - just one file
            model.uploadOne(id, 0, false);
        }
        else
        {
            // Proceed upload - for add - could be multiple files
            var addedAtt = model.uploadMulti(itemdb);
            if (addedAtt.Count > 0)
                id = (addedAtt[0] as Hashtable)!["id"].toInt();
            fw.flash("added", 1);
            location = base_url;
        }

        // if select in popup - return json
        ps["id"] = id;
        if (id > 0)
        {
            var item_new = model.one(id);
            ps["success"] = true;
            ps["icode"] = item_new["icode"];
            ps["url"] = model.getUrl(id);
            ps["url_preview"] = model.getUrlPreview(id);
            ps["iname"] = item_new["iname"];
            ps["is_image"] = item_new["is_image"];
            ps["fsize"] = item_new["fsize"];
            ps["ext"] = item_new["ext"];
        }
        else
            ps["success"] = false;

        fw.flash("success", "File uploaded");

        return this.afterSave(true, id, is_new, FW.ACTION_SHOW_FORM, location, ps);
    }

    public override void Validate(int id, Hashtable item)
    {
        // only require file during first upload
        // only require iname during update
        Hashtable itemdb;
        if (id > 0)
        {
            itemdb = model.one(id);
            validateRequired(id, item, Utils.qw(required_fields));
        }
        else
        {
            itemdb = [];
            itemdb["fsize"] = "0";
        }

        if (itemdb["fsize"].toInt() == 0)
        {
            var files = fw.request?.Form?.Files;
            if (files == null || files.Count == 0 || files[0] == null || files[0].Length == 0)
            {
                fw.FormErrors["file1"] = "NOFILE";
            }
        }

        this.validateCheckResult();
    }

    public Hashtable SelectAction()
    {
        Hashtable ps = [];
        string category_icode = reqs("category");
        int att_categories_id = reqi("att_categories_id");

        Hashtable where = [];
        where["status"] = 0;
        if (category_icode.Length > 0)
        {
            var att_cat = fw.model<AttCategories>().oneByIcode(category_icode);
            if (att_cat.Count > 0)
            {
                att_categories_id = att_cat["id"].toInt();
                where["att_categories_id"] = att_categories_id;
            }
        }
        if (att_categories_id > 0)
            where["att_categories_id"] = att_categories_id;

        var is_json = fw.isJsonExpected();
        ArrayList rows = db.array(model.table_name, where, "add_time desc");
        foreach (Hashtable row in rows)
        {
            row["url"] = model.getUrl(row);
            if (is_json)
                model.filterForJson(row);
        }
        ps["att_dr"] = rows;
        ps["select_att_categories_id"] = fw.model<AttCategories>().listSelectOptions();
        ps["att_categories_id"] = att_categories_id;
        ps["XSS"] = fw.Session("XSS");
        ps["_json"] = true; // enable json for Vue

        return ps;
    }
}