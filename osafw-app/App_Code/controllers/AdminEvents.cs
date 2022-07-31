// Events Log Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class AdminEventsController : FwAdminController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    protected FwEvents model = new();
    protected Users model_users = new();

    public override void init(FW fw)
    {
        base.init(fw);
        model0 = model;
        model.init(fw);
        model_users.init(fw);
        required_fields = "iname"; // default required fields, space-separated
        base_url = "/Admin/Events"; // base url for the controller

        search_fields = "!item_id iname fields";
        list_sortdef = "iname asc";
        list_sortmap = Utils.qh("id|id iname|iname add_time|add_time");

        list_view = model.log_table_name;
    }

    public override Hashtable initFilter(string session_key = null)
    {
        base.initFilter(session_key);

        if (reqs("dofilter").Length==0 && (string)list_filter["date"] == "")
            list_filter["date"] = DateUtils.Date2Str(DateTime.Now);
        return null;
    }

    public override void setListSearch()
    {
        base.setListSearch();

        if (!string.IsNullOrEmpty((string)list_filter["events_id"]))
        {
            list_where += " and events_id = @f_events_id";
            list_where_params["f_events_id"] = Utils.f2int(list_filter["events_id"]);
        }
        if (!string.IsNullOrEmpty((string)list_filter["users_id"]))
        {
            list_where += " and users_id = @f_users_id";
            list_where_params["f_users_id"] = Utils.f2int(list_filter["users_id"]);
        }
        if (!string.IsNullOrEmpty((string)list_filter["date"]))
        {
            list_where += " and add_time >= @f_date and add_time < DATEADD(DAY, 1, @f_date)";
            list_where_params["f_date"] = Utils.f2date(list_filter["date"]);
        }
    }

    public override void setListSearchStatus()
    {
    }

    public override void getListRows()
    {
        base.getListRows();

        foreach (Hashtable row in list_rows)
        {
            //logger(row);
            row["user"] = model_users.one(Utils.f2int(row["add_users_id"]));
            row["event"] = model.one(Utils.f2int(row["events_id"]));
        }
    }

    public override Hashtable IndexAction()
    {
        var ps = base.IndexAction();

        ps["filter_select_events"] = model.listSelectOptions();
        ps["filter_select_users"] = model_users.listSelectOptions();

        return ps;
    }
}