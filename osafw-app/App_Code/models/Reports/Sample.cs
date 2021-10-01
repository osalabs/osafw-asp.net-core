// Sample report - shows Event Log
//
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw
{
    public class ReportSample : ReportBase
    {
        public ReportSample() : base()
        {

            // override report render options if necessary
            render_options["landscape"] = false;
        }

        // define report filters in Me.f (available in report templates as f[...])
        // filter defaults can be Set here
        public override Hashtable getReportFilters()
        {
            Hashtable result = new();
            if (!f.ContainsKey("from_date") && !f.ContainsKey("to_date"))
                // set default filters
                f["from_date"] = DateUtils.Date2Str(DateTime.Now.AddDays(-30));// last 30 days
            if (!string.IsNullOrEmpty((string)f["from_date"]) || !string.IsNullOrEmpty((string)f["to_date"]))
                f["is_dates"] = true;

            result["select_events"] = fw.model<FwEvents>().listSelectOptions();

            return result;
        }

        public override Hashtable getReportData()
        {
            Hashtable ps = new();

            // apply filters from Me.f
            string where = " ";
            Hashtable where_params = new();
            if (!string.IsNullOrEmpty((string)f["from_date"]))
            {
                where += " and el.add_time>=@from_date";
                where_params["@from_date"] = f["from_date"];
            }
            if (System.DateTime.TryParse((string)f["to_date"], out DateTime to_date))
            {
                where += " and el.add_time<@to_date";
                where_params["@to_date"] = to_date.AddDays(1);
            }
                
            if (!string.IsNullOrEmpty((string)f["events_id"]))
            {
                where += " and el.events_id=@events_id";
                where_params["@events_id"] = f["events_id"];
            }            

            // define query
            string sql;

            sql = "select top 20 el.*, e.iname  as event_name, u.fname, u.lname " + "  from [events] e, event_log el " +
                  "       LEFT OUTER JOIN users u ON (u.id=el.add_users_id)" + 
                  " where el.events_id=e.id" + where + 
                  " order by el.id desc";
            var rows = db.arrayp(sql, where_params).toArrayList();
            ps["rows"] = rows;
            ps["count"] = rows.Count;

            // perform calculations and add additional info for each result row
            foreach (Hashtable row in rows)
            {
                //row["event"] = fw.model<FwEvents>().one(Utils.f2int(row["events_id"]));
                ps["total_ctr"] = _calcPerc(rows); //if you need calculate "perc" for each row based on row["ctr"]
            }

            return ps;
        }
    }
}