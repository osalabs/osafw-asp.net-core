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

    protected Spages model;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<Spages>();
        model0 = model;

        // initialization
        base_url = "/Admin/Spages";
        required_fields = "iname";
        save_fields = "iname idesc idesc_left idesc_right head_att_id template prio meta_keywords meta_description custom_css custom_js redirect_url";

        search_fields = "url iname idesc";
        list_sortdef = "iname asc";   // default sorting: name, asc|desc direction
        list_sortmap = Utils.qh("id|id iname|iname pub_time|pub_time upd_time|upd_time status|status url|url");
    }

    public override Hashtable IndexAction()
    {
        // get filters from the search form
        Hashtable f = this.initFilter();

        this.setListSorting();
        this.setListSearch();
        this.setListSearchStatus();

        if ((string)list_filter["sortby"] == "iname" && (string)list_filter["s"] == "" & ((string)this.list_filter["status"] == "" || (string)this.list_filter["status"] == "0"))
        {
            // show tree only if sort by title and no search and status by all or active
            this.list_count = Utils.f2long(db.valuep("select count(*) from " + db.qid(model.table_name) + " where " + this.list_where, this.list_where_params));
            if (this.list_count > 0)
            {
                // build pages tree
                ArrayList pages_tree = model.tree(this.list_where, this.list_where_params, "parent_id, prio desc, iname");
                this.list_rows = model.getPagesTreeList(pages_tree, 0);

                // apply LIMIT
                var pagesize = Utils.f2int(this.list_filter["pagesize"]);
                var pagenum = Utils.f2int(this.list_filter["pagenum"]);
                if (this.list_count > pagesize)
                {
                    ArrayList subset = new();
                    int start_offset = pagenum * pagesize;

                    for (int i = start_offset; i <= Math.Min(start_offset + pagesize, this.list_rows.Count) - 1; i++)
                        subset.Add(this.list_rows[i]);
                    this.list_rows = subset;
                }

                this.list_pager = FormUtils.getPager(this.list_count, pagenum, pagesize);
            }
            else
            {
                this.list_rows = new ArrayList();
                this.list_pager = new ArrayList();
            }
        }
        else
            // if order not by iname or search performed - display plain page list using  Me.get_list_rows()
            this.getListRows();

        // add/modify rows from db if necessary
        foreach (Hashtable row in this.list_rows)
            row["full_url"] = model.getFullUrl(Utils.f2int(row["id"]));

        var ps = this.setPS();

        return ps;
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        // set new form defaults here if any
        if (!string.IsNullOrEmpty(reqs("parent_id")))
        {
            this.form_new_defaults = new Hashtable();
            this.form_new_defaults["parent_id"] = reqi("parent_id");
        }
        Hashtable ps = base.ShowFormAction(id);

        var item = (Hashtable)ps["i"];
        string where = " status<>@status ";
        ArrayList pages_tree = model.tree(where, DB.h("status", FwModel.STATUS_DELETED), "parent_id, prio desc, iname");
        ps["select_options_parent_id"] = model.getPagesTreeSelectHtml(Utils.f2str(item["parent_id"]), pages_tree);

        ps["parent_url"] = model.getFullUrl(Utils.f2int(item["parent_id"]));
        ps["full_url"] = model.getFullUrl(Utils.f2int(item["id"]));

        ps["parent"] = model.one(Utils.f2int(item["parent_id"]));

        if (!Utils.isEmpty(item["head_att_id"]))
            ps["att"] = fw.model<Att>().one(Utils.f2int(item["head_att_id"]));

        if (id > 0)
            ps["subpages"] = model.listChildren(id);

        return ps;
    }

    public override Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM;

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in save_fields ");

        Hashtable item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        Hashtable item_old = model.one(id);
        // for non-home page enable some fields
        string save_fields2 = this.save_fields;
        if ((string)item_old["is_home"] != "1")
            save_fields2 += " parent_id url status pub_time";

        // auto-generate url if it's empty
        if ((string)item["url"] == "")
        {
            item["url"] = item["iname"];
            item["url"] = Regex.Replace((string)item["url"], @"^\W+", "");
            item["url"] = Regex.Replace((string)item["url"], @"\W+$", "");
            item["url"] = Regex.Replace((string)item["url"], @"\W+", "-");
            if ((string)item["url"] == "")
            {
                if (id > 0)
                    item["url"] = "page-" + id;
                else
                    item["url"] = "page-" + Utils.uuid();
            }
        }

        Validate(id, item);
        // load old record if necessary

        Hashtable itemdb = FormUtils.filter(item, save_fields2);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes);
        itemdb["prio"] = Utils.f2int(itemdb["prio"]);

        // if no publish time defined - publish it now
        if ((string)itemdb["pub_time"] == "")
            itemdb["pub_time"] = DB.NOW;

        id = this.modelAddOrUpdate(id, itemdb);

        if ((string)item_old["is_home"] == "1")
            FwCache.remove("home_page"); // reset home page cache if Home page changed

        return this.afterSave(success, id, is_new);
    }

    public override void Validate(int id, Hashtable item)
    {
        bool result = this.validateRequired(item, this.required_fields);

        if (result && model.isExistsByUrl((string)item["url"], Utils.f2int(item["parent_id"]), id))
            fw.FormErrors["url"] = "EXISTS";

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