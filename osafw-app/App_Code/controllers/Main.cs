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

    /// <summary>
    /// Returns recent daily activity counts with provider-neutral date bucketing and C# label formatting.
    /// </summary>
    /// <param name="logTypeCode">Optional log type code filter, such as <c>login</c>.</param>
    /// <param name="scopedUsersId">When nonzero, limits activity to the current dashboard user.</param>
    /// <returns>Rows with <c>idate</c>, <c>ivalue</c>, and chart label fields.</returns>
    private FwList listDailyActivityCounts(string logTypeCode = "", int scopedUsersId = 0)
    {
        var dateExpr = db.sqlDateExpr("al.idate");
        var sql = "select " + dateExpr + " as idate, count(*) as ivalue "
            + " from activity_logs al, log_types lt "
            + " where al.log_types_id=lt.id";
        var p = new FwDict();
        if (!string.IsNullOrEmpty(logTypeCode))
        {
            sql += " and lt.icode=@log_type_code";
            p["@log_type_code"] = logTypeCode;
        }
        if (scopedUsersId != 0)
        {
            sql += " and al.users_id=@users_id";
            p["@users_id"] = scopedUsersId;
        }

        sql += " group by " + dateExpr + " order by " + dateExpr + " desc";
        var rows = db.arrayp(db.limit(sql, 14), p);
        rows.Sort((a, b) => string.CompareOrdinal(a["idate"], b["idate"]));

        FwList result = rows;
        foreach (FwDict row in result)
        {
            var dt = row["idate"].toDate();
            row["ilabel"] = dt.Month + "/" + dt.Day;
        }

        return result;
    }

    private void addTableHeadersAndCols(FwDict pane, FwList rows, string dateTimeFields = "")
    {
        pane["rows"] = rows;
        var headers = new FwList();
        pane["headers"] = headers;
        if (rows.Count == 0 || rows[0] is not FwDict firstRow)
            return;

        var dateFields = Utils.qh(dateTimeFields);
        var keys = firstRow.Keys;
        var fields = new string[keys.Count];
        keys.CopyTo(fields, 0);
        foreach (var key in fields)
            headers.Add(new FwDict() { { "field_name", key } });
        foreach (FwDict row in rows)
        {
            FwList cols = [];
            foreach (var fieldname in fields)
            {
                if (dateFields.ContainsKey(fieldname))
                    row[fieldname] = fw.formatUserDateTime(row[fieldname]);
                cols.Add(new FwDict()
                {
                    {"row",row},
                    {"field_name",fieldname},
                    {"data",row[fieldname]}
                });
            }
            row["cols"] = cols;
        }
    }

    public FwDict IndexAction()
    {

        FwDict ps = [];

        FwDict one;
        FwDict panes = [];
        ps["panes"] = panes;
        bool isAssistantEnabled = fw.model<Settings>().readBool("ASSISTANT_ENABLED");
        ps["is_assistant_enabled"] = isAssistantEnabled;

        const int DIFF_DAYS = -7;
        // init const int[] STATUSES with single value FwModel.STATUS_ACTIVE
        var STATUSES = new int[] { FwModel.STATUS_ACTIVE };
        var scopedUsersId = fw.userAccessLevel >= Users.ACL_SITEADMIN ? 0 : (fw.userId > 0 ? fw.userId : -1);

        one = [];
        one["type"] = "bignum";
        one["title"] = "Pages";
        one["url"] = "/Admin/Spages";
        one["value"] = fw.model<Spages>().getCount(STATUSES, userId: scopedUsersId);
        one["value_class"] = "text-warning";
        one["badge_value"] = Utils.percentChange(fw.model<Spages>().getCount(STATUSES, DIFF_DAYS, scopedUsersId), fw.model<Spages>().getCount(STATUSES, DIFF_DAYS * 2, scopedUsersId));
        one["badge_class"] = "text-bg-warning";
        one["icon"] = "bi-file-earmark-richtext";
        panes["plate1"] = one;

        one = [];
        one["type"] = "bignum";
        one["title"] = "Uploads";
        one["url"] = "/Admin/Att";
        one["value"] = fw.model<Att>().getCount(STATUSES, userId: scopedUsersId);
        one["value_class"] = "text-info";
        one["badge_value"] = Utils.percentChange(fw.model<Att>().getCount(STATUSES, DIFF_DAYS, scopedUsersId), fw.model<Att>().getCount(STATUSES, DIFF_DAYS * 2, scopedUsersId));
        one["badge_class"] = "text-bg-info";
        one["icon"] = "bi-cloud-upload";
        panes["plate2"] = one;

        one = [];
        one["type"] = "bignum";
        one["title"] = "Users";
        one["url"] = "/Admin/Users";
        one["value"] = fw.model<Users>().getCount(STATUSES, userId: scopedUsersId, userField: "id");
        one["value_class"] = "text-success";
        one["badge_value"] = Utils.percentChange(fw.model<Users>().getCount(STATUSES, DIFF_DAYS, scopedUsersId, "id"), fw.model<Users>().getCount(STATUSES, DIFF_DAYS * 2, scopedUsersId, "id"));
        one["badge_class"] = "text-bg-success";
        one["icon"] = "bi-people";
        panes["plate3"] = one;

        one = [];
        one["type"] = "bignum";
        one["title"] = "Events";
        one["url"] = "/Admin/Reports/sample";
        one["value"] = fw.model<FwActivityLogs>().getCountByLogIType(FwLogTypes.ITYPE_SYSTEM, STATUSES, userId: scopedUsersId);
        one["value_class"] = "";
        one["badge_value"] = Utils.percentChange(fw.model<FwActivityLogs>().getCountByLogIType(FwLogTypes.ITYPE_SYSTEM, STATUSES, DIFF_DAYS, scopedUsersId), fw.model<FwActivityLogs>().getCountByLogIType(FwLogTypes.ITYPE_SYSTEM, STATUSES, DIFF_DAYS * 2, scopedUsersId));
        one["badge_class"] = "text-bg-secondary";
        one["icon"] = "bi-clock";
        panes["plate4"] = one;

        if (isAssistantEnabled)
        {
            one = [];
            one["type"] = "assistant";
            one["title"] = "AI Assistant";
            panes["assistant"] = one;
        }

        one = [];
        one["type"] = "barchart";
        one["title"] = "Logins per day";
        one["id"] = "logins_per_day";
        // one["url") ] "/Admin/Reports/sample"
        one["rows"] = listDailyActivityCounts("login", scopedUsersId);
        panes["barchart"] = one;

        one = [];
        one["type"] = "piechart";
        one["title"] = "Users by Type";
        one["id"] = "user_types";
        // one["url") ] "/Admin/Reports/sample"
        FwList rows = db.arrayp("select access_level, count(*) as ivalue from users where status=0 group by access_level order by count(*) desc", DB.h());
        one["rows"] = rows;
        foreach (FwDict row in rows)
            row["ilabel"] = FormUtils.selectTplName("/common/sel/access_level.sel", row["access_level"].toStr());
        panes["piechart"] = one;

        one = [];
        one["type"] = "table";
        one["title"] = "Last Events";
        // one["url") ] "/Admin/Reports/sample"
        var lastEventsSql = "select al.idate as " + db.qid("On") + ", " + db.sqlConcat("fe.iname", db.q(" "), "lt.iname", db.q(" "), "al.idesc") + " as Event " +
            " from activity_logs al, log_types lt, fwentities fe " +
            " where al.log_types_id=lt.id" +
            "   and fe.id=al.fwentities_id";
        var lastEventsParams = DB.h();
        if (scopedUsersId != 0)
        {
            lastEventsSql += " and al.users_id=@users_id";
            lastEventsParams["@users_id"] = scopedUsersId;
        }
        rows = db.arrayp(db.limit(lastEventsSql + " order by al.id desc", 10), lastEventsParams);
        addTableHeadersAndCols(one, rows, "On");
        panes["tabledata"] = one;

        // Example of using Report class to generate html block
        //one = [];
        //one["type"] = "html";
        //one["title"] = "Last Events Report";
        ////from yesterday
        //var f = new FwRow
        //{
        //    { "from_date", DateUtils.Date2Str(DateTime.Now) }
        //};
        //var ps_rep = new FwRow
        //{
        //    { "IS_SUPPRESS_TITLE", true }
        //};
        //one["html"] = FwReportsBase.createHtml(fw, "sample", f, ps_rep);
        //panes["htmldata"] = one;

        one = [];
        one["type"] = "linechart";
        one["title"] = "Events per day";
        one["id"] = "eventsctr";
        // one["url") ] "/Admin/Reports/sample"
        one["rows"] = listDailyActivityCounts(scopedUsersId: scopedUsersId);
        panes["linechart"] = one;

        // Example for area chart
        //one = [];
        //one["type"] = "areachart";
        //one["title"] = "Events Area";
        //one["id"] = "events_area";
        //one["rows"] = db.arrayp("with zzz as ("
        //    + db.limit("select CAST(al.idate as date) as idate, count(*) as ivalue "
        //    + " from activity_logs al, log_types lt "
        //    + "where al.log_types_id=lt.id"
        //    + " group by CAST(al.idate as date) order by CAST(al.idate as date) desc", 14)
        //    + ")"
        //    + " select CONCAT(MONTH(idate),'/',DAY(idate)) as ilabel, ivalue from zzz order by idate", DB.h());
        //panes["areachart"] = one;

        one = [];
        one["type"] = "progress";
        one["title"] = "Active Users";
        long totalUsers = fw.model<Users>().getCount(userId: scopedUsersId, userField: "id");
        long activeUsers = fw.model<Users>().getCount(STATUSES, userId: scopedUsersId, userField: "id");
        one["percent"] = totalUsers == 0 ? 0 : activeUsers * 100 / totalUsers;
        one["progress_class"] = "bg-success";
        panes["progress"] = one;

        return ps;
    }

    public void UIThemeAction(string form_id)
    {
        fw.Session("ui_theme", form_id);
        var fields = new FwDict() { { "ui_theme", form_id } };

        fw.model<Users>().update(fw.userId, fields);

        fw.redirect(base_url);
    }

    public void UIModeAction(string form_id)
    {
        fw.Session("ui_mode", form_id);
        fw.model<Users>().update(fw.userId, new FwDict() { { "ui_mode", form_id } });

        fw.redirect(base_url);
    }
}
