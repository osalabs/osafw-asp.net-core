// Admin Users controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections.Generic;
using System.Linq;

namespace osafw;

public class AdminUsersController : FwDynamicController
{
    public static new int access_level = Users.ACL_ADMIN;

    protected Users model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        // use if config doesn't contains model name
        // model0 = fw.model(Of Users)()
        // model = model0

        base_url = "/Admin/Users";
        this.loadControllerConfig();
        model = (Users)model0;
        db = model.getDB(); // model-based controller works with model's db

        model_related = fw.model<Users>();

        list_sortmap.Remove("last_logins");
    }

    public override void setListSearch()
    {
        base.setListSearch();

        if (!Utils.isEmpty(list_filter["access_level"]))
        {
            list_where += " and access_level=@access_level";
            list_where_params["access_level"] = list_filter["access_level"];
        }
    }

    public override FwDict setPS(FwDict? ps = null)
    {
        ps = base.setPS(ps);
        ps["is_roles"] = model.isRoles();
        return ps;
    }

    /// <summary>
    /// Load list rows and enrich them with last login activity for mini charts.
    /// </summary>
    /// <remarks>Used by the admin users list to render the "Last Logins" column without per-row queries.</remarks>
    public override void getListRows()
    {
        base.getListRows();

        if (list_rows.Count == 0)
            return;

        addLastLoginSeries(list_rows);
    }

    /// <summary>
    /// Adds a 7-day login series to each list row for chart rendering.
    /// </summary>
    /// <param name="rows">The list rows to augment with <c>last_logins</c> series data.</param>
    private void addLastLoginSeries(FwList rows)
    {
        var userIds = rows.Select(row => row["id"].toInt()).Where(id => id > 0).Distinct().ToList();
        if (userIds.Count == 0)
            return;

        var dateRange = buildLastLoginDates();
        var countsByUser = getLoginCountsByUser(userIds, dateRange[0]);

        foreach (FwDict row in rows)
        {
            var userId = row["id"].toInt();
            var dailyCounts = new int[dateRange.Count];

            if (countsByUser.TryGetValue(userId, out var userCounts))
            {
                for (int i = 0; i < dateRange.Count; i++)
                {
                    if (userCounts.TryGetValue(dateRange[i], out var count))
                        dailyCounts[i] = count;
                }
            }

            row["last_logins"] = string.Join(",", dailyCounts);
        }
    }

    /// <summary>
    /// Builds the last 7 dates (inclusive) used for login activity charts.
    /// </summary>
    /// <returns>Chronological list of dates from 6 days ago through today.</returns>
    private static List<DateTime> buildLastLoginDates()
    {
        var startDate = DateTime.Today.AddDays(-6);
        var dates = new List<DateTime>(7);
        for (int i = 0; i < 7; i++)
            dates.Add(startDate.AddDays(i).Date);
        return dates;
    }

    /// <summary>
    /// Fetches login counts per user per day starting from the provided date.
    /// </summary>
    /// <param name="userIds">User IDs to include in the aggregation.</param>
    /// <param name="fromDate">The earliest date to include in the results.</param>
    /// <returns>Mapping of user ID to per-day login counts.</returns>
    private Dictionary<int, Dictionary<DateTime, int>> getLoginCountsByUser(List<int> userIds, DateTime fromDate)
    {
        var sqlParams = new FwDict
        {
            ["@from_date"] = fromDate,
            ["@login_icode"] = FwLogTypes.ICODE_USERS_LOGIN
        };

        var inParams = new List<string>(userIds.Count);
        for (int i = 0; i < userIds.Count; i++)
        {
            var paramName = "@user_id_" + i;
            sqlParams[paramName] = userIds[i];
            inParams.Add(paramName);
        }

        // Build an IN clause with parameters to avoid per-row queries.
        var sql = "select al.item_id as users_id, CAST(al.idate as date) as login_date, count(*) as login_count "
            + "from activity_logs al "
            + "inner join log_types lt on lt.id=al.log_types_id "
            + "where lt.icode=@login_icode "
            + "and al.item_id in (" + string.Join(",", inParams) + ") "
            + "and al.idate >= @from_date "
            + "group by al.item_id, CAST(al.idate as date)";

        var rows = db.arrayp(sql, sqlParams);
        var result = new Dictionary<int, Dictionary<DateTime, int>>();

        foreach (FwDict row in rows)
        {
            var userId = row["users_id"].toInt();
            var loginDate = row["login_date"].toDate().Date;
            var count = row["login_count"].toInt();

            if (!result.TryGetValue(userId, out var userCounts))
            {
                userCounts = new Dictionary<DateTime, int>();
                result[userId] = userCounts;
            }

            userCounts[loginDate] = count;
        }

        return result;
    }

    public override FwDict ShowFormAction(int id = 0)
    {
        var ps = base.ShowFormAction(id)!;
        var item = (FwDict)ps["i"]!;
        ps["att"] = fw.model<Att>().one(item["att_id"]);

        ps["is_roles"] = model.isRoles();
        ps["roles_link"] = model.listLinkedRoles(id);

        return ps;
    }

    public override FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in Controller.save_fields");

        if (reqb("refresh"))
        {
            fw.routeRedirect(FW.ACTION_SHOW_FORM, [id]);
            return null;
        }

        FwDict item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        item["email"] = item["ehack"]; // just because Chrome autofills fields too agressively

        Validate(id, item);
        // load old record if necessary
        // var itemOld = model0.one(id);

        FwDict itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());

        itemdb["pwd"] = itemdb["pwd"].toStr().Trim();
        if (Utils.isEmpty(itemdb["pwd"]))
            itemdb.Remove("pwd");

        id = this.modelAddOrUpdate(id, itemdb);

        model.updateLinkedRoles(id, reqh("roles_link"));

        if (fw.userId == id)
            model.reloadSession(id);

        return this.afterSave(success, id, is_new);
    }

    public override void Validate(int id, FwDict item)
    {
        bool result = true;
        result &= validateRequired(id, item, Utils.qw(required_fields));
        if (!result)
            fw.FormErrors["REQ"] = 1;

        if (result && model.isExists(item["email"].toStr(), id))
        {
            result = false;
            fw.FormErrors["ehack"] = "EXISTS";
        }
        if (result && !FormUtils.isEmail(item["email"].toStr()))
        {
            result = false;
            fw.FormErrors["ehack"] = "EMAIL";
        }

        // uncomment if project requires good password strength
        // If result AndAlso item.ContainsKey("pwd") AndAlso model.scorePwd(item["pwd"]) <= 60 Then
        // result = False
        // fw.FERR["pwd"] = "BAD"
        // End If

        // If result AndAlso Not SomeOtherValidation() Then
        // result = False
        // FW.FERR("other field name") = "HINT_ERR_CODE"
        // End If

        this.validateCheckResult();
    }

    // cleanup session for current user and re-login as user from id
    // check access - only users with higher level may login as lower leve
    public void SimulateAction(int id)
    {
        var user = model.one(id);
        if (user.Count == 0)
            throw new NotFoundException("Wrong User ID");
        if (user["access_level"].toInt() >= fw.userAccessLevel)
            throw new AuthException("Access Denied. Cannot simulate user with higher access level");

        fw.logActivity(FwLogTypes.ICODE_USERS_SIMULATE, FwEntities.ICODE_USERS, id);

        model.doLogin(id);

        fw.redirect(fw.config("LOGGED_DEFAULT_URL").toStr());
    }

    public FwDict SendPwdAction(int id)
    {
        FwDict ps = [];

        ps["success"] = model.sendPwdReset(id);
        ps["err_msg"] = fw.last_error_send_email;
        ps["_json"] = true;
        return ps;
    }

    // for migration to hashed passwords
    public void HashPasswordsAction()
    {
        rw("hashing passwords");
        var rows = db.array(model.table_name, [], "id");
        foreach (var row in rows)
        {
            if (row["pwd"].Substring(0, 2) == "$2")
                continue; // already hashed
            var hashed = model.hashPwd(row["pwd"]);
            db.update(model.table_name, new FwDict() { { "pwd", hashed } }, new FwDict() { { "id", row["id"] } });
        }
        rw("done");
    }

    public void ResetMFAAction(int id)
    {
        model.update(id, DB.h("mfa_secret", null));
        //fw.flash("success", "Multi-Factor Authentication ");
        fw.redirect($"{base_url}/ShowForm/{id}/edit");
    }
}
