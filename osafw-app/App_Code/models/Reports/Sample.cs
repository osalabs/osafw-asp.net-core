// Sample report - shows Event Log
//
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class SampleReport : FwReports
{
    public SampleReport() : base()
    {
        //access_level = Users.ACL_MANAGER; //override if necessay

        // override report render options if necessary
        render_options["landscape"] = false;

        // setup sorting for each column
        list_sortdef = "idate desc";
        list_sortmap = Utils.qh("id|id idate|idate event_name|event_name entity_name|entity_name item_id|item_id idesc|idesc payload|payload user|fname,lname");
    }

    // define report filters in Me.f (available in report templates as f[...])
    // filter defaults can be Set here
    public override void setFilters()
    {
        Hashtable result = [];
        if (!f.ContainsKey("from_date") && !f.ContainsKey("to_date"))
            // set default filters
            f["from_date"] = DateUtils.Date2Str(DateTime.Now.AddDays(-30));// last 30 days
        if (!Utils.isEmpty(f["from_date"]) || !Utils.isEmpty(f["to_date"]))
            f["is_dates"] = true;

        f_data["select_events"] = fw.model<FwLogTypes>().listSelectOptions();
        f_data["select_fwentities"] = fw.model<FwEntities>().listSelectOptions();
        f_data["select_users"] = fw.model<Users>().listSelectOptions();
    }

    public override void getData()
    {
        Hashtable ps = [];

        setListSorting();

        // apply filters from Me.f
        string where = " ";
        Hashtable where_params = [];
        if (!Utils.isEmpty(f["from_date"]))
        {
            where += " and al.add_time>=@from_date";
            where_params["@from_date"] = f["from_date"];
        }
        if (System.DateTime.TryParse((string)f["to_date"], out DateTime to_date))
        {
            where += " and al.add_time<@to_date";
            where_params["@to_date"] = to_date.AddDays(1);
        }

        if (!Utils.isEmpty(f["events_id"]))
        {
            where += " and al.log_types_id=@events_id";
            where_params["@events_id"] = f["events_id"];
        }

        if (!Utils.isEmpty(f["fwentities_id"]))
        {
            where += " and al.fwentities_id=@fwentities_id";
            where_params["@fwentities_id"] = f["fwentities_id"];
        }

        if (!Utils.isEmpty(f["users_id"]))
        {
            where += " and al.add_users_id=@users_id";
            where_params["@users_id"] = f["users_id"];
        }

        if (!Utils.isEmpty(f["s"]))
        {
            //search in item_id, idesc, payload
            where += " and (al.item_id=@item_id OR al.idesc like @slike OR al.payload like @slike)";
            where_params["@item_id"] = Utils.f2int(f["s"]);
            where_params["@slike"] = "%" + f["s"] + "%";
        }

        // define query
        // REMEMBER to filter out deleted items for each table, i.e. add call andNotDeleted([alias])
        string sql;
        sql = @$"select al.*
                , lt.iname as event_name
                , et.iname as entity_name
                , u.fname
                , u.lname
                from activity_logs al
                     INNER JOIN log_types lt ON (lt.id=al.log_types_id)
                     INNER JOIN fwentities et ON (et.id=al.fwentities_id)
                     LEFT OUTER JOIN users u ON (u.id=al.add_users_id {andNotDeleted("u.")})
                where 1=1
                {where}
                order by {list_orderby}";
        sql = db.limit(sql, 20); //limit to first results only
        list_rows = db.arrayp(sql, where_params);
        list_count = list_rows.Count;

        // perform calculations and add additional info for each result row
        foreach (Hashtable row in list_rows)
        {
            //row["entity"] = fw.model<FwEntities>().one(Utils.f2int(row["fwentities_id"]));
            ps["total_ctr"] = _calcPerc(list_rows); //if you need calculate "perc" for each row based on row["ctr"]
            //if row["payload"] contains password/pass/pwd - hide it
            var payload = Utils.f2str(row["payload"]);
            if (payload.Contains("pass") || payload.Contains("pwd"))
                row["payload"] = "********";
        }
    }
}