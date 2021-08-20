using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace osafw
{

    public class FwEvents : FwModel
    {
        public string log_table_name = "event_log";

        public FwEvents() : base()
        {
            table_name = "events";
        }

        // just return first row by icode field (you may want to make it unique)
        public Hashtable oneByIcode(string icode)
        {
            Hashtable where = new();
            where["icode"] = icode;
            return db.row(table_name, where);
        }

        public void log(string ev_icode, int item_id = 0, int item_id2 = 0, string iname = "", int records_affected = 0, Hashtable changed_fields = null)
        {
            Hashtable hEV = oneByIcode(ev_icode);
            if (!hEV.ContainsKey("id"))
            {
                fw.logger(LogLevel.WARN, "No event defined for icode=[", ev_icode, "], auto-creating");
                hEV = new Hashtable
                {
                    ["icode"] = ev_icode,
                    ["iname"] = ev_icode,
                    ["idesc"] = "auto-created"
                };
                hEV["id"] = this.add(hEV);
            }

            Hashtable fields = new()
            {
                ["events_id"] = hEV["id"],
                ["item_id"] = item_id,
                ["item_id2"] = item_id2,
                ["iname"] = iname,
                ["records_affected"] = records_affected
            };
            if (changed_fields != null)
                fields["fields"] = Utils.jsonEncode(changed_fields);
            fields["add_users_id"] = Users.id;
            db.insert(log_table_name, fields);
        }

        // just for short form call
        // Public Overloads Sub logEvent(ev_icode As String, item_id As Integer)
        // log(ev_icode, item_id, 0, "", 0)
        // End Sub

        /// <summary>
        /// leave in only those item keys, which are apsent/different from itemold
        /// </summary>
        /// <param name="item"></param>
        /// <param name="itemold"></param>
        public Hashtable changes_only(Hashtable item, Hashtable itemold)
        {
            Hashtable result = new();
            object datenew;
            object dateold;
            object vnew;
            object vold;
            foreach (var key in item.Keys)
            {
                vnew = item[key];
                vold = itemold[key];

                datenew = Utils.f2date(vnew);
                dateold = Utils.f2date(vold);
                if (datenew != null && dateold != null)
                {
                    // it's dates - only compare DATE part, not time as all form inputs are dates without times
                    vnew = System.Convert.ToDateTime(datenew).ToShortDateString();
                    vold = System.Convert.ToDateTime(dateold).ToShortDateString();
                }

                // If Not itemold.ContainsKey(key) _
                // OrElse vnew Is Nothing AndAlso vold IsNot Nothing _
                // OrElse vnew IsNot Nothing AndAlso vold Is Nothing _
                // OrElse vnew IsNot Nothing AndAlso vold IsNot Nothing _
                // AndAlso vnew.ToString() <> vold.ToString() _
                // Then
                if (!itemold.ContainsKey(key) || Utils.f2str(vnew) != Utils.f2str(vold))
                    // logger("****:" & key)
                    // logger(TypeName(vnew) & " - " & vnew & " - " & datenew)
                    // logger(TypeName(vold) & " - " & vold & " - " & dateold)
                    result[key] = item[key];
            }
            return result;
        }

        /// <summary>
        /// return true if any of passed fields changed
        /// </summary>
        /// <param name="item1"></param>
        /// <param name="item2"></param>
        /// <param name="fields">qw-list of fields</param>
        /// <returns>false if no chagnes in passed fields or fields are empty</returns>
        public bool is_changed(Hashtable item1, Hashtable item2, string fields)
        {
            var result = false;
            var afields = Utils.qw(fields);
            foreach (var fld in afields)
            {
                if (item1.ContainsKey(fld) && item2.ContainsKey(fld) && Utils.f2str(item1[fld]) != Utils.f2str(item2[fld]))
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        // check if 2 dates (without time) chagned
        public bool is_changed_date(object date1, object date2)
        {
            var dt1 = Utils.f2date(date1);
            var dt2 = Utils.f2date(date2);

            if (dt1 != null || dt2 != null)
            {
                if (dt1 != null && dt2 != null)
                {
                    // both set - compare dates
                    if (DateUtils.Date2SQL((DateTime)dt1) != DateUtils.Date2SQL((DateTime)dt2))
                        return true;
                }
                else
                    // one set, one no - chagned
                    return true;
            }
            else
            {
            }

            return false;
        }
    }

}