// User Lists Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class MyListsController : FwAdminController
{
    public static new int access_level = Users.ACL_MEMBER;

    protected UserLists model;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<UserLists>();
        model0 = model;

        // initialization
        base_url = "/My/Lists";
        required_fields = "entity iname";
        save_fields = "entity iname idesc status";

        search_fields = "iname idesc";
        list_sortdef = "iname asc";   // default sorting: name, asc|desc direction
        list_sortmap = Utils.qh("id|id entity|entity iname|iname add_time|add_time");

        related_id = reqs("related_id");
    }

    public override Hashtable setPS(Hashtable ps = null)
    {
        ps = base.setPS(ps);
        ps["select_entities"] = model.listSelectOptionsEntities();
        return ps;
    }

    public override Hashtable initFilter(string session_key = null)
    {
        var result = base.initFilter(session_key);
        if (!this.list_filter.ContainsKey("entity"))
            this.list_filter["entity"] = related_id;
        return this.list_filter;
    }

    public override void setListSearchStatus()
    {
        if (!string.IsNullOrEmpty((string)list_filter["status"]))
        {
            this.list_where += " and status=@status";
            this.list_where_params["@status"] = db.qi(list_filter["status"]);
        }
        else
        {
            // if no status passed - by default show all non-deleted
            this.list_where += " and status<>@status";
            this.list_where_params["@status"] = FwModel.STATUS_DELETED;
        }
    }

    public override void setListSearch()
    {
        list_where = " add_users_id=@add_users_id";
        list_where_params["@add_users_id"] = fw.userId;

        base.setListSearch();

        if (!string.IsNullOrEmpty((string)list_filter["entity"]))
        {
            this.list_where += " and entity=@entity";
            this.list_where_params["@entity"] = list_filter["entity"];
        }
    }

    public override void getListRows()
    {
        base.getListRows();

        foreach (Hashtable row in this.list_rows)
            row["ctr"] = model.countItems(Utils.f2int(row["id"]));
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        this.form_new_defaults = new();
        this.form_new_defaults["entity"] = related_id;
        return base.ShowFormAction(id);
    }

    public override Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in Controller.save_fields");

        Hashtable item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        Validate(id, item);
        // load old record if necessary
        // Dim item_old As Hashtable = model0.one(id)

        Hashtable itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes);

        id = this.modelAddOrUpdate(id, itemdb);

        if (is_new && item.ContainsKey("item_id"))
        {
            // item_id could contain comma-separated ids
            var hids = Utils.commastr2hash((string)item["item_id"]);
            if (hids.Count > 0)
            {
                // if item id passed - link item with the created list
                foreach (string sitem_id in hids.Keys)
                {
                    var item_id = Utils.f2int(sitem_id);
                    if (item_id > 0)
                        model.addItems(id, item_id);
                }
            }
        }

        return this.afterSave(success, id, is_new);
    }

    public Hashtable ToggleListAction(int id)
    {
        var item_id = reqi("item_id");
        var ps = new Hashtable();

        var user_lists = fw.model<UserLists>().one(id);
        if (item_id == 0 || user_lists.Count == 0 || Utils.f2int(user_lists["add_users_id"]) != fw.userId)
            throw new UserException("Wrong Request");

        var res = fw.model<UserLists>().toggleItemList(id, item_id);
        ps["iname"] = user_lists["iname"];
        ps["action"] = (res ? "added" : "removed");

        return afterSave(true, ps);
    }

    // request item_id - could be one id, or comma-separated ids
    public Hashtable AddToListAction(int id)
    {
        throw new ApplicationException("zzzz");
        Hashtable items = Utils.commastr2hash(reqs("item_id"));

        var user_lists = fw.model<UserLists>().one(id);
        if (user_lists.Count == 0 || Utils.f2int(user_lists["add_users_id"]) != fw.userId)
            throw new UserException("Wrong Request");

        foreach (string key in items.Keys)
        {
            var item_id = Utils.f2int(key);
            if (item_id > 0)
                fw.model<UserLists>().addItemList(id, item_id);
        }

        return afterSave(true);
    }

    // request item_id - could be one id, or comma-separated ids
    public Hashtable RemoveFromListAction(int id)
    {
        Hashtable items = Utils.commastr2hash(reqs("item_id"));

        var user_lists = fw.model<UserLists>().one(id);
        if (user_lists.Count == 0 || Utils.f2int(user_lists["add_users_id"]) != fw.userId)
            throw new UserException("Wrong Request");

        foreach (string key in items.Keys)
        {
            var item_id = Utils.f2int(key);
            if (item_id > 0)
                fw.model<UserLists>().delItemList(id, item_id);
        }

        return afterSave(true);
    }
}