// User Lists Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Linq;

namespace osafw;

public class MyListsController : FwAdminController
{
    public static new int access_level = Users.ACL_MEMBER;

    protected UserLists model = null!;

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

        is_readonly = false;//allow update my stuff
    }

    public override FwDict setPS(FwDict? ps = null)
    {
        ps = base.setPS(ps);
        ps["select_entities"] = model.listSelectOptionsEntities();
        return ps;
    }

    public override FwDict initFilter(string? session_key = null)
    {
        base.initFilter(session_key);
        if (!this.list_filter.ContainsKey("entity"))
            this.list_filter["entity"] = related_id;
        return this.list_filter;
    }

    public override void setListSearchStatus()
    {
        var statusValues = getStatusFilterValues();
        if (statusValues.Count > 0)
        {
            if (!fw.model<Users>().isAccessLevel(Users.ACL_SITEADMIN) && statusValues.Contains(FwModel.STATUS_DELETED))
            {
                // Non-admins cannot filter to deleted; fallback to active if no other status remains.
                statusValues = statusValues.Where(status => status != FwModel.STATUS_DELETED).ToList();
                if (statusValues.Count == 0)
                    statusValues.Add(FwModel.STATUS_ACTIVE);
            }

            this.list_where += " and status IN (@status_list)";
            this.list_where_params["status_list"] = statusValues;
        }
        else
        {
            // if no status passed - by default show all non-deleted
            this.list_where += " and status<>@status";
            this.list_where_params["status"] = FwModel.STATUS_DELETED;
        }
    }

    public override void setListSearch()
    {
        list_where = " add_users_id=@add_users_id";
        list_where_params["@add_users_id"] = fw.userId;

        base.setListSearch();

        if (!Utils.isEmpty(list_filter["entity"]))
        {
            this.list_where += " and entity=@entity";
            this.list_where_params["@entity"] = list_filter["entity"];
        }
    }

    public override void getListRows()
    {
        base.getListRows();

        foreach (FwDict row in this.list_rows)
            row["ctr"] = model.countItems(row["id"].toInt());
    }

    public override FwDict? ShowFormAction(int id = 0)
    {
        form_new_defaults = new() { ["entity"] = related_id };
        return base.ShowFormAction(id);
    }

    public override FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in Controller.save_fields");

        FwDict item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        Validate(id, item);
        // load old record if necessary
        // var itemOld = model0.one(id);

        FwDict itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());

        id = this.modelAddOrUpdate(id, itemdb);

        if (is_new && item.TryGetValue("item_id", out object? value))
        {
            // item_id could contain comma-separated ids
            var hids = Utils.commastr2hash(value.toStr());
            if (hids.Count > 0)
            {
                // if item id passed - link item with the created list
                foreach (string sitem_id in hids.Keys)
                {
                    var item_id = sitem_id.toInt();
                    if (item_id > 0)
                        model.addItems(id, item_id);
                }
            }
        }

        return this.afterSave(success, id, is_new);
    }

    public FwDict? ToggleListAction(int id)
    {
        var item_id = reqi("item_id");
        var ps = new FwDict();

        var user_lists = fw.model<UserLists>().one(id);
        if (item_id == 0 || user_lists.Count == 0 || user_lists["add_users_id"].toInt() != fw.userId)
            throw new UserException("Wrong Request");

        var res = fw.model<UserLists>().toggleItemList(id, item_id);
        ps["iname"] = user_lists["iname"];
        ps["action"] = (res ? "added" : "removed");

        return afterSave(true, ps);
    }

    // request item_id - could be one id, or comma-separated ids
    public FwDict? AddToListAction(int id)
    {
        FwDict items = Utils.commastr2hash(reqs("item_id"));

        var user_lists = fw.model<UserLists>().one(id);
        if (user_lists.Count == 0 || user_lists["add_users_id"].toInt() != fw.userId)
            throw new UserException("Wrong Request");

        foreach (string key in items.Keys)
        {
            var item_id = key.toInt();
            if (item_id > 0)
                fw.model<UserLists>().addItemList(id, item_id);
        }

        return afterSave(true);
    }

    // request item_id - could be one id, or comma-separated ids
    public FwDict? RemoveFromListAction(int id)
    {
        FwDict items = Utils.commastr2hash(reqs("item_id"));

        var user_lists = fw.model<UserLists>().one(id);
        if (user_lists.Count == 0 || user_lists["add_users_id"].toInt() != fw.userId)
            throw new UserException("Wrong Request");

        foreach (string key in items.Keys)
        {
            var item_id = key.toInt();
            if (item_id > 0)
                fw.model<UserLists>().delItemList(id, item_id);
        }

        return afterSave(true);
    }
}
