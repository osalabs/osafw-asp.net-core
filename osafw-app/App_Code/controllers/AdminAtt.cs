// Admin Att controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class AdminAttController : FwAdminController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected Att model = new();

    public override void init(FW fw)
    {
        base.init(fw);
        model.init(fw);
        model0 = model;

        base_url = "/Admin/Att"; // base url for the controller
        required_fields = "iname"; // default required fields, space-separated
        save_fields = "att_categories_id iname status";

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
            row["fsize_human"] = Utils.bytes2str(row["fsize"].toLong());
            if (row["is_image"].toInt() == 1)
            {
                row["url"] = model.getUrl(row["id"].toInt());
                row["url_s"] = model.getUrl(row["id"].toInt(), "s");
            }

            var att_categories_id = row["att_categories_id"].toInt();
            if (att_categories_id > 0)
                row["cat"] = fw.model<AttCategories>().one(att_categories_id);
        }
    }

    public override Hashtable IndexAction()
    {
        var ps = base.IndexAction();

        ps["select_att_categories_ids"] = fw.model<AttCategories>().listSelectOptions();
        return ps;
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        var ps = base.ShowFormAction(id);
        var item = (Hashtable)ps["i"];

        ps["fsize_human"] = Utils.bytes2str(item["fsize"].toLong());
        ps["url"] = model.getUrl(id);
        if (item["is_image"].toInt() == 1)
            ps["url_m"] = model.getUrl(id, "m");

        ps["select_options_att_categories_id"] = fw.model<AttCategories>().listSelectOptions();

        return ps;
    }

    public override Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        Hashtable ps = new();
        Hashtable item = reqh("item");
        var is_new = (id == 0);

        Validate(id, item);
        // load old record if necessary
        // Dim itemold As Hashtable = model.one(id)

        Hashtable itemdb = FormUtils.filter(item, save_fields);
        if (Utils.isEmpty(itemdb["iname"]))
            itemdb["iname"] = "new file upload";

        if (id > 0)
        {
            model.update(id, itemdb);
            fw.flash("updated", 1);

            // Proceed upload - for edit - just one file
            model.uploadOne(id, 0, false);
        }
        else
        {
            // Proceed upload - for add - could be multiple files
            var addedAtt = model.uploadMulti(itemdb);
            if (addedAtt.Count > 0)
                id = (int)((Hashtable)addedAtt[0])["id"];
            fw.flash("added", 1);
        }

        // if select in popup - return json
        ps["id"] = id;
        if (id > 0)
        {
            item = model.one(id);
            ps["success"] = true;
            ps["url"] = model.getUrl(id);
            ps["url_preview"] = model.getUrlPreview(id);
            ps["iname"] = item["iname"];
            ps["is_image"] = item["is_image"];
            ps["ext"] = item["ext"];
        }
        else
            ps["success"] = false;

        fw.flash("success", "File uploaded");

        return this.afterSave(true, id, is_new, FW.ACTION_SHOW_FORM, "", ps);
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
            itemdb = new();
            itemdb["fsize"] = "0";
        }

        if (itemdb["fsize"].toInt() == 0)
        {
            if (fw.request.Form.Files.Count == 0 || fw.request.Form.Files[0] == null || fw.request.Form.Files[0].Length == 0)
            {
                fw.FormErrors["file1"] = "NOFILE";
            }
        }

        this.validateCheckResult();
    }

    public Hashtable SelectAction()
    {
        Hashtable ps = new();
        string category_icode = reqs("category");
        int att_categories_id = reqi("att_categories_id");

        Hashtable where = new();
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
            row["url_preview"] = model.getUrlPreview(row);
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