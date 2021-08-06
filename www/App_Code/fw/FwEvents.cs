using osafw_asp.net_core.fw;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp.net_core.App_Code.fw
{
    public class FwEvents : FwModel
    {
        public String log_table_name = "event_log";

        public FwEvents() : base()
        {
            table_name = "events";
        }

        // just return first row by icode field (you may want to make it unique)
        public Hashtable oneByIcode(String icode)
        {
            Hashtable where = new Hashtable();
            where["icode"] = icode;
            return db.row(table_name, where);
        }

        public void log(String ev_icode, int item_id = 0, int item_id2 = 0, String iname = "", int records_affected = 0)
        {
            Hashtable hEV = oneByIcode(ev_icode);
            if (!hEV.ContainsKey("id"))
            {
                fw.logger(LogLevel.WARN, "No event defined for icode=[", ev_icode, "], auto-creating");
                hEV = new Hashtable();
                hEV["icode"] = ev_icode;
                hEV["iname"] = ev_icode;
                hEV["idesc"] = "auto-created";
                hEV["id"] = add(hEV);
            }

            Hashtable fields = new Hashtable();
            fields["events_id"] = hEV["id"];
            fields["item_id"] = item_id;
            fields["item_id2"] = item_id2;
            fields["iname"] = iname;
            fields["records_affected"] = records_affected;
            fields["add_users_id"] = (fw.modelOf(typeof(Users)) as Users).meId();
            db.insert(log_table_name, fields);
        }

    // just for short form call
    // Public Overloads Sub logEvent(ev_icode As String, item_id As Integer)
    //    log(ev_icode, item_id, 0, "", 0)
    // End Sub

    }
}
