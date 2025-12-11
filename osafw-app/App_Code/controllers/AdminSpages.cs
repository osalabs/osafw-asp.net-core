// Static Pages Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Text.RegularExpressions;

namespace osafw;

public class AdminSpagesController : FwAdminController
{
    public static new int access_level = Users.ACL_MANAGER;

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
        list_sortdef = "iname asc";   // default sorting: name, asc|desc direction
        list_sortmap = Utils.qh("id|id iname|iname pub_time|pub_time upd_time|upd_time status|status url|url");
    }

    public override void getListRows()
    {
        if (list_filter["sortby"].toStr() == "iname"
            && list_filter["s"].toStr() == ""
            && (this.list_filter["status"].toStr() == "" || this.list_filter["status"].toStr() == "0"))
        {
            // show tree only if sort by title and no search and status by all or active
            this.list_count = db.valuep("select count(*) from " + db.qid(model.table_name) +
                " where " + this.list_where, this.list_where_params).toLong();
            if (this.list_count > 0)
            {
                // build pages tree
                FwList pages_tree = model.tree(this.list_where, this.list_where_params, "parent_id, prio desc, iname");
                this.list_rows = model.getPagesTreeList(pages_tree, 0);

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
        var parent_id = reqi("parent_id");

        // set new form defaults here if any
        if (parent_id > 0)
        {
            var parent = model.one(parent_id);
            this.form_new_defaults = new FwDict
            {
                ["parent_id"] = parent_id
            };
        }
        var ps = base.ShowFormAction(id) ?? [];

        var item = ps["i"] as FwDict ?? [];
        string where = " status<>@status ";
        FwList pages_tree = model.tree(where, DB.h("status", FwModel.STATUS_DELETED), "parent_id, prio desc, iname");
        ps["select_options_parent_id"] = model.getPagesTreeSelectHtml(item["parent_id"].toStr(), pages_tree);

        ps["parent_url"] = model.getFullUrl(item["parent_id"].toInt());
        ps["full_url"] = model.getFullUrl(id);

        ps["parents"] = model.listParents(id);

        ps["parent"] = model.one(item["parent_id"].toInt());

        if (!Utils.isEmpty(item["head_att_id"]))
            ps["att"] = fw.model<Att>().one(item["head_att_id"]);

        if (id > 0)
            ps["subpages"] = model.listChildren(id);

        return ps;
    }

    public override FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM;

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in save_fields ");

        FwDict item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        var item_old = model.one(id);
        // for non-home page enable some fields
        string save_fields2 = this.save_fields;
        if (item_old["is_home"] != "1")
            save_fields2 += " parent_id url status pub_time";

        // auto-generate url if it's empty
        if (item["url"].toStr() == "")
        {
            item["url"] = item["iname"];
            item["url"] = Regex.Replace(item["url"].toStr(), @"^\W+", "");
            item["url"] = Regex.Replace(item["url"].toStr(), @"\W+$", "");
            item["url"] = Regex.Replace(item["url"].toStr(), @"\W+", "-");
            if (item["url"].toStr() == "")
            {
                if (id > 0)
                    item["url"] = "page-" + id;
                else
                    item["url"] = "page-" + Utils.uuid();
            }
        }

        Validate(id, item);
        // load old record if necessary

        FwDict itemdb = FormUtils.filter(item, save_fields2);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());
        itemdb["prio"] = itemdb["prio"].toInt();

        // if no publish time defined - publish it now
        if (itemdb["pub_time"].toStr() == "")
            itemdb["pub_time"] = DB.NOW;

        logger("itemdb: ", itemdb);
        id = this.modelAddOrUpdate(id, itemdb);

        if (item_old["is_home"] == "1")
            FwCache.remove("home_page"); // reset home page cache if Home page changed

        return this.afterSave(success, id, is_new);
    }

    public override void Validate(int id, FwDict item)
    {
        bool result = this.validateRequired(id, item, this.required_fields);

        if (result && model.isExistsByUrl(item["url"].toStr(), item["parent_id"].toInt(), id))
            fw.FormErrors["url"] = "EXISTS";

        if (result)
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

        //if (result && model0.isExists(item["iname"], id)){
        //    fw.FERR["iname"] = "EXISTS";
        //}

        //if (result && !SomeOtherValidation())
        //{
        //    fw.FERR["other field name"] = "HINT_ERR_CODE";
        //}

        this.validateCheckResult();
    }
}