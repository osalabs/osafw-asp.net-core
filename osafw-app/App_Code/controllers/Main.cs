// Main Page for Logged user controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;
using System.Security.AccessControl;

namespace osafw
{
    public class MainController : FwController
    {
        public static new int access_level = Users.ACL_MEMBER;

        public override void init(FW fw)
        {
            base.init(fw);
            base_url = "/Main";
        }

        public Hashtable IndexAction()
        {

            Hashtable ps = new();

            Hashtable one;
            Hashtable panes = new();
            ps["panes"] = panes;

            one = new Hashtable();
            one["type"] = "bignum";
            one["title"] = "Pages";
            one["url"] = "/Admin/Spages";
            one["value"] = fw.model<Spages>().getCount();
            panes["plate1"] = one;

            one = new Hashtable();
            one["type"] = "bignum";
            one["title"] = "Uploads";
            one["url"] = "/Admin/Att";
            one["value"] = fw.model<Att>().getCount();
            panes["plate2"] = one;

            one = new Hashtable();
            one["type"] = "bignum";
            one["title"] = "Users";
            one["url"] = "/Admin/Users";
            one["value"] = fw.model<Users>().getCount();
            panes["plate3"] = one;

            one = new Hashtable();
            one["type"] = "bignum";
            one["title"] = "Demo items";
            one["url"] = "/Admin/DemosDynamic";
            one["value"] = fw.model<Demos>().getCount();
            panes["plate4"] = one;

            one = new Hashtable();
            one["type"] = "barchart";
            one["title"] = "Logins per day";
            one["id"] = "logins_per_day";
            // one["url") ] "/Admin/Reports/sample"
            one["rows"] = db.arrayp("with zzz as ("
                + " select TOP 14 CAST(el.add_time as date) as idate, count(*) as ivalue from events ev, event_log el where ev.icode='login' and el.events_id=ev.id"
                + " group by CAST(el.add_time as date) order by CAST(el.add_time as date) desc)"
                + " select CONCAT(MONTH(idate),'/',DAY(idate)) as ilabel, ivalue from zzz order by idate", DB.h());
            panes["barchart"] = one;

            one = new Hashtable();
            one["type"] = "piechart";
            one["title"] = "Users by Type";
            one["id"] = "user_types";
            // one["url") ] "/Admin/Reports/sample"
            ArrayList rows = db.arrayp("select access_level, count(*) as ivalue from users group by access_level order by count(*) desc", DB.h());
            one["rows"] = rows;
            foreach (Hashtable row in rows)
                row["ilabel"] = FormUtils.selectTplName("/common/sel/access_level.sel", (string)row["access_level"]);
            panes["piechart"] = one;

            one = new Hashtable();
            one["type"] = "table";
            one["title"] = "Last Events";
            // one["url") ] "/Admin/Reports/sample"
            rows = db.arrayp("select TOP 10 el.add_time as [On], ev.iname as Event from events ev, event_log el where el.events_id=ev.id order by el.id desc", DB.h());
            one["rows"] = rows;
            var headers = new ArrayList();
            one["headers"] = headers;
            if (rows.Count > 0)
            {
                //var fields = ((Hashtable)rows[0]).Keys.Cast<string>.ToArray();
                var fields = ((Hashtable)rows[0]).Keys;
                foreach (var key in fields)
                    headers.Add(new Hashtable() { { "field_name", key } });
                foreach (Hashtable row in rows)
                {
                    ArrayList cols = new();
                    foreach (var fieldname in fields)
                        cols.Add(new Hashtable()
                    {
                        {
                            "row",
                            row
                        },
                        {
                            "field_name",
                            fieldname
                        },
                        {
                            "data",
                            row[fieldname]
                        }
                    });
                    row["cols"] = cols;
                }
            }
            panes["tabledata"] = one;


            one = new Hashtable();
            one["type"] = "linechart";
            one["title"] = "Events per day";
            one["id"] = "eventsctr";
            // one["url") ] "/Admin/Reports/sample"
            one["rows"] = db.arrayp("with zzz as ("
                + " select TOP 14 CAST(el.add_time as date) as idate, count(*) as ivalue from events ev, event_log el where el.events_id=ev.id"
                + " group by CAST(el.add_time as date) order by CAST(el.add_time as date) desc)"
                + " select CONCAT(MONTH(idate),'/',DAY(idate)) as ilabel, ivalue from zzz order by idate", DB.h());
            panes["linechart"] = one;

            return ps;
        }

        public void ThemeAction(string form_id)
        {
            fw.Session("theme", form_id);

            fw.redirect(base_url);
        }
    }
}