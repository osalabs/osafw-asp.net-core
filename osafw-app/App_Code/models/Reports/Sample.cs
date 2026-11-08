// Sample report - shows Event Log
//
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class SampleReport : FwReportsBase
{
    public SampleReport() : base()
    {
        //access_level = Users.ACL_MANAGER; //override if necessay

        // override report render options if necessary
        render_options["landscape"] = false;
        render_options["margin_bottom"] = "12mm";

        // setup sorting for each column
        list_sortdef = "idate desc";
        list_sortmap = Utils.qh("id|id idate|idate event_name|event_name entity_name|entity_name item_id|item_id idesc|idesc payload|payload user|fname,lname");
    }

    public override bool isSchedulingSupported => true;
    public override string schedulingCode => "Sample";
    public override string schedulingTitle => "Sample Report (Activity Log)";

    public override FwList describeScheduledFilters(FwList saved)
    {
        var result = base.describeScheduledFilters(saved);
        foreach (var parameter in result)
        {
            var id = parameter["value"].toInt();
            if (id <= 0)
                continue;
            parameter["value"] = parameter["name"].toStr() switch
            {
                "events_id" => fw.model<FwLogTypes>().iname(id),
                "fwentities_id" => fw.model<FwEntities>().iname(id),
                "users_id" => fw.model<Users>().iname(id),
                _ => parameter["value"]
            };
        }
        return result;
    }

    protected override FwList schedulingParameters() =>
    [
        new()
        {
            ["name"] = "from_date",
            ["label"] = "From date",
            ["type"] = "date"
        },
        new()
        {
            ["name"] = "to_date",
            ["label"] = "To date",
            ["type"] = "date"
        },
        new()
        {
            ["name"] = "events_id",
            ["label"] = "Event",
            ["type"] = "int"
        },
        new()
        {
            ["name"] = "fwentities_id",
            ["label"] = "Entity",
            ["type"] = "int"
        },
        new()
        {
            ["name"] = "users_id",
            ["label"] = "User",
            ["type"] = "int"
        },
        new()
        {
            ["name"] = "s",
            ["label"] = "Search",
            ["type"] = "text"
        }
    ];

    // define report filters in Me.f (available in report templates as f[...])
    // filter defaults can be Set here
    public override void setFilters()
    {
        base.setFilters();

        FwDict result = [];
        if (!f.ContainsKey("from_date") && !f.ContainsKey("to_date"))
            // set default filters
            f["from_date"] = DateUtils.Date2Str(DateTime.Now.AddDays(-30), fw.userDateFormat);// last 30 days
        if (!Utils.isEmpty(f["from_date"]) || !Utils.isEmpty(f["to_date"]))
            f["is_dates"] = true;

        f_data["select_events"] = fw.model<FwLogTypes>().listSelectOptions();
        f_data["select_fwentities"] = fw.model<FwEntities>().listSelectOptions();
        f_data["select_users"] = fw.model<Users>().listSelectOptions();
    }

    public override void getData()
    {
        base.getData();

        setListSorting();

        // apply filters from Me.f
        string where = " ";
        FwDict where_params = [];
        var fromDateSql = DateUtils.Str2SQL(f["from_date"].toStr(), fw.userDateFormat);
        if (DateUtils.SQL2Date(fromDateSql) is DateTime fromDate)
        {
            where += " and al.add_time>=@from_date";
            where_params["@from_date"] = fromDate;
            f["from_date"] = DateUtils.Date2Str(fromDate, fw.userDateFormat);
        }
        var toDateSql = DateUtils.Str2SQL(f["to_date"].toStr(), fw.userDateFormat);
        if (DateUtils.SQL2Date(toDateSql) is DateTime toDate)
        {
            where += " and al.add_time<@to_date";
            where_params["@to_date"] = toDate.AddDays(1);
            f["to_date"] = DateUtils.Date2Str(toDate, fw.userDateFormat);
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
            where_params["@item_id"] = f["s"].toInt();
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
        sql = db.limit(sql, 250); // Allow multiple printed pages while keeping the sample bounded.
        list_rows = db.arrayp(sql, where_params);
        list_count = list_rows.Count;

        if (is_scheduled_snapshot)
        {
            var appliedParams = new FwList();
            foreach (var def in schedulingParameters())
            {
                var name = def["name"].toStr();
                if (Utils.isEmpty(f[name]))
                    continue;

                appliedParams.Add(new FwDict
                {
                    ["name"] = name,
                    ["label"] = def["label"],
                    ["value"] = f[name]
                });
            }
            ps["applied_params"] = appliedParams;
        }

        // perform calculations and add additional info for each result row
        foreach (FwDict row in list_rows)
        {
            //row["entity"] = fw.model<FwEntities>().one(row["fwentities_id"]);
            //ps["total_ctr"] = _calcPerc(list_rows); //if you need calculate "perc" for each row based on row["ctr"]
            // if row["payload"] contains password/pass/pwd - hide it
            var payload = row["payload"].toStr();
            if (payload.Contains("pass") || payload.Contains("pwd"))
                row["payload"] = "********";
        }
    }
}
