﻿// User Filters Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class MyFiltersController : FwAdminController
{
    public static new int access_level = Users.ACL_MEMBER;

    protected UserFilters model;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<UserFilters>();
        model0 = model;

        // initialization
        base_url = "/My/Filters";
        required_fields = "iname";
        save_fields = "icode iname status";
        save_fields_checkboxes = "is_system";

        search_fields = "iname";
        list_sortdef = "iname asc";   // default sorting: name, asc|desc direction
        list_sortmap = Utils.qh("id|id iname|iname add_time|add_time");

        related_id = reqs("related_id");
    }

    public override Hashtable initFilter(string session_key = null)
    {
        base.initFilter(session_key);
        if (!this.list_filter.ContainsKey("icode"))
            this.list_filter["icode"] = related_id;
        return this.list_filter;
    }

    public override void setListSearch()
    {
        // only logged user lists
        list_where = " add_users_id=@add_users_id";
        list_where_params["@add_users_id"] = fw.userId;

        base.setListSearch();

        if (!Utils.isEmpty(list_filter["icode"]))
        {
            this.list_where += " and icode=@icode";
            this.list_where_params["@icode"] = list_filter["icode"];
        }
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        this.form_new_defaults = new();
        this.form_new_defaults["icode"] = related_id;
        var ps = base.ShowFormAction(id);
        ps["is_admin"] = fw.userAccessLevel == Users.ACL_ADMIN;
        return ps;
    }

    public override Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in Controller.save_fields");

        var item = reqh("item");
        var success = true;
        var is_new = (id == 0);
        var is_overwrite = reqi("is_overwrite") == 1;

        if (is_new)
            required_fields += " icode";
        Validate(id, item);
        // load old record if necessary
        Hashtable item_old = model0.one(id);

        // also check that this filter is user's filter (cannot override system filter)
        if (item_old.Count > 0 && Utils.f2int(item_old["is_system"]) == 1)
            throw new UserException("Cannot overwrite system filter");

        Hashtable itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());

        if (is_new || is_overwrite)
            // read new filter data from session
            itemdb["idesc"] = Utils.jsonEncode(fw.Session("_filter_" + item["icode"]));

        id = this.modelAddOrUpdate(id, itemdb);

        return this.afterSave(success, id, is_new);
    }
}