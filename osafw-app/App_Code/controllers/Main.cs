// Main Page for Logged user controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class MainController : FwController
{
    public static new int access_level = Users.ACL_MEMBER;

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Main";
    }

    public override void checkAccess()
    {
        // add custom actions to permissions mapping
        access_actions_to_permissions = new() {
            { "UITheme", Permissions.PERMISSION_LIST },
            { "UIMode", Permissions.PERMISSION_LIST },
        };
        base.checkAccess();
    }

    public Hashtable IndexAction()
    {

        Hashtable ps = new();

        Hashtable one;
        Hashtable panes = new();
        ps["panes"] = panes;

        const int DIFF_DAYS = -7;
        // init const int[] STATUSES with single value FwModel.STATUS_ACTIVE
        var STATUSES = new int[] { FwModel.STATUS_ACTIVE };

        one = new Hashtable();
        one["type"] = "bignum";
        one["title"] = "Pages";
        one["url"] = "/Admin/Spages";
        one["value"] = fw.model<Spages>().getCount(STATUSES);
        one["value_class"] = "text-warning";
        one["badge_value"] = Utils.percentChange(fw.model<Spages>().getCount(STATUSES, DIFF_DAYS), fw.model<Spages>().getCount(STATUSES, DIFF_DAYS * 2));
        one["badge_class"] = "text-bg-warning";
        one["icon"] = "pages";
        panes["plate1"] = one;

        one = new Hashtable();
        one["type"] = "bignum";
        one["title"] = "Uploads";
        one["url"] = "/Admin/Att";
        one["value"] = fw.model<Att>().getCount(STATUSES);
        one["value_class"] = "text-info";
        one["badge_value"] = Utils.percentChange(fw.model<Att>().getCount(STATUSES, DIFF_DAYS), fw.model<Att>().getCount(STATUSES, DIFF_DAYS * 2));
        one["badge_class"] = "text-bg-info";
        one["icon"] = "uploads";
        panes["plate2"] = one;

        one = new Hashtable();
        one["type"] = "bignum";
        one["title"] = "Users";
        one["url"] = "/Admin/Users";
        one["value"] = fw.model<Users>().getCount(STATUSES);
        one["value_class"] = "text-success";
        one["badge_value"] = Utils.percentChange(fw.model<Users>().getCount(STATUSES, DIFF_DAYS), fw.model<Users>().getCount(STATUSES, DIFF_DAYS * 2));
        one["badge_class"] = "text-bg-success";
        one["icon"] = "users";
        panes["plate3"] = one;

        one = new Hashtable();
        one["type"] = "bignum";
        one["title"] = "Events";
        one["url"] = "/Admin/Reports/sample";
        one["value"] = fw.model<FwActivityLogs>().getCountByLogIType(FwLogTypes.ITYPE_SYSTEM, STATUSES);
        one["value_class"] = "";
        one["badge_value"] = Utils.percentChange(fw.model<FwActivityLogs>().getCountByLogIType(FwLogTypes.ITYPE_SYSTEM, STATUSES, DIFF_DAYS), fw.model<FwActivityLogs>().getCountByLogIType(FwLogTypes.ITYPE_SYSTEM, STATUSES, DIFF_DAYS * 2));
        one["badge_class"] = "text-bg-secondary";
        one["icon"] = "events";
        panes["plate4"] = one;

        one = new Hashtable();
        one["type"] = "barchart";
        one["title"] = "Logins per day";
        one["id"] = "logins_per_day";
        // one["url") ] "/Admin/Reports/sample"
        one["rows"] = db.arrayp("with zzz as ("
            + db.limit("select CAST(al.idate as date) as idate, count(*) as ivalue "
            + " from activity_logs al, log_types lt "
            + " where lt.icode='login' and al.log_types_id=lt.id"
            + " group by CAST(al.idate as date) order by CAST(al.idate as date) desc", 14)
            + ")"
            + " select CONCAT(MONTH(idate),'/',DAY(idate)) as ilabel, ivalue from zzz order by idate", DB.h());
        panes["barchart"] = one;

        one = new Hashtable();
        one["type"] = "piechart";
        one["title"] = "Users by Type";
        one["id"] = "user_types";
        // one["url") ] "/Admin/Reports/sample"
        ArrayList rows = db.arrayp("select access_level, count(*) as ivalue from users where status=0 group by access_level order by count(*) desc", DB.h());
        one["rows"] = rows;
        foreach (Hashtable row in rows)
            row["ilabel"] = FormUtils.selectTplName("/common/sel/access_level.sel", (string)row["access_level"]);
        panes["piechart"] = one;

        one = new Hashtable();
        one["type"] = "table";
        one["title"] = "Last Events";
        // one["url") ] "/Admin/Reports/sample"
        rows = db.arrayp(db.limit("select al.idate as " + db.qid("On") + ", CONCAT(fe.iname, ' ', lt.iname, ' ', al.idesc) as Event " +
            " from activity_logs al, log_types lt, fwentities fe " +
            " where al.log_types_id=lt.id" +
            "   and fe.id=al.fwentities_id" +
            " order by al.id desc", 10), DB.h());
        one["rows"] = rows;
        var headers = new ArrayList();
        one["headers"] = headers;
        if (rows.Count > 0)
        {
            var keys = ((Hashtable)rows[0]).Keys;
            var fields = new string[keys.Count];
            keys.CopyTo(fields, 0);
            foreach (var key in fields)
                headers.Add(new Hashtable() { { "field_name", key } });
            foreach (Hashtable row in rows)
            {
                ArrayList cols = new();
                foreach (var fieldname in fields)
                {
                    cols.Add(new Hashtable()
                    {
                        {"row",row},
                        {"field_name",fieldname},
                        {"data",row[fieldname]}
                    });
                }
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
            + db.limit("select CAST(al.idate as date) as idate, count(*) as ivalue "
            + " from activity_logs al, log_types lt "
            + "where al.log_types_id=lt.id"
            + " group by CAST(al.idate as date) order by CAST(al.idate as date) desc", 14)
            + ")"
            + " select CONCAT(MONTH(idate),'/',DAY(idate)) as ilabel, ivalue from zzz order by idate", DB.h());
        panes["linechart"] = one;

        return ps;
    }

    public void UIThemeAction(string form_id)
    {
        fw.Session("ui_theme", form_id);
        var fields = new Hashtable() { { "ui_theme", form_id } };
        if (form_id == "30")
        {
            fields["ui_mode"] = "10"; //for blue theme - enforce light color mode
            fw.Session("ui_mode", "10");
        }

        fw.model<Users>().update(fw.userId, fields);

        fw.redirect(base_url);
    }

    public void UIModeAction(string form_id)
    {
        fw.Session("ui_mode", form_id);
        fw.model<Users>().update(fw.userId, new Hashtable() { { "ui_mode", form_id } });

        fw.redirect(base_url);
    }
}