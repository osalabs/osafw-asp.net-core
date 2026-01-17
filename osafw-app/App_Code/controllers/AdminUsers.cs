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
    /// remove non-database chart fields from the sortable list to avoid invalid SQL ordering
    /// </summary>
    /// <returns>mapping of sortable list fields to their SQL equivalents</returns>
    public override FwDict getViewListSortmap()
    {
        var sortmap = base.getViewListSortmap();
        sortmap.Remove("last_logins");
        return sortmap;
    }

    /// <summary>
    /// extend view screen payload with avatar and roles data so the view layout can match the edit layout
    /// </summary>
    /// <param name="id">user id for the record being viewed</param>
    /// <returns>parsepage dictionary with additional user metadata for the view template</returns>
    public override FwDict? ShowAction(int id = 0)
    {
        var ps = base.ShowAction(id);
        if (ps == null)
            return null;

        var item = (FwDict)ps["i"]!;
        ps["att"] = fw.model<Att>().one(item["att_id"]);
        ps["is_roles"] = model.isRoles();
        ps["roles_link"] = model.listLinkedRoles(id);

        return ps;
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

    /// <summary>
    /// load list rows and append last 7-day login chart data for each user to render mini bar charts
    /// </summary>
    public override void getListRows()
    {
        base.getListRows();

        if (list_rows.Count == 0)
            return;

        var user_ids = list_rows.Select(row => row["id"].toInt()).Where(id => id > 0).ToList();
        if (user_ids.Count == 0)
            return;

        var start_date = DateTime.Today.AddDays(-6);
        var login_counts = listLoginCountsByUser(user_ids, start_date);
        var labels = buildLoginLabels(start_date, 7);

        foreach (DBRow row in list_rows)
        {
            var user_id = row["id"].toInt();
            var values = new int[labels.Count];
            if (login_counts.TryGetValue(user_id, out var series))
            {
                for (var i = 0; i < labels.Count; i++)
                {
                    var day = start_date.AddDays(i);
                    if (series.TryGetValue(day, out var count))
                        values[i] = count;
                }
            }

            row["last_logins"] = Utils.jsonEncode(new FwDict
            {
                { "labels", labels },
                { "values", values }
            });
        }
    }

    /// <summary>
    /// collect login counts per user per day so list view can render last-login charts without N+1 queries
    /// </summary>
    /// <param name="user_ids">list of user ids currently visible in the list screen</param>
    /// <param name="start_date">earliest date to include in the series (inclusive)</param>
    /// <returns>map of user id to a date-count dictionary for logins</returns>
    protected Dictionary<int, Dictionary<DateTime, int>> listLoginCountsByUser(IList<int> user_ids, DateTime start_date)
    {
        var result = new Dictionary<int, Dictionary<DateTime, int>>();
        var sql = @"select al.users_id,
                           CAST(al.idate as date) as idate,
                           count(*) as ivalue
                      from activity_logs al
                      inner join log_types lt on lt.id=al.log_types_id
                     where lt.icode=@icode
                       and al.users_id in (@user_ids)
                       and al.idate >= @start_date
                  group by al.users_id, CAST(al.idate as date)";

        var rows = db.arrayp(sql, DB.h
        {
            { "icode", FwLogTypes.ICODE_USERS_LOGIN },
            { "user_ids", user_ids },
            { "start_date", start_date }
        });

        foreach (DBRow row in rows)
        {
            var user_id = row["users_id"].toInt();
            var day = row["idate"].toDate().Date;
            if (!result.TryGetValue(user_id, out var series))
            {
                series = new Dictionary<DateTime, int>();
                result[user_id] = series;
            }

            series[day] = row["ivalue"].toInt();
        }

        return result;
    }

    /// <summary>
    /// build human-friendly day labels for the last-login mini chart
    /// </summary>
    /// <param name="start_date">first date to include in the label list</param>
    /// <param name="days">number of days to include</param>
    /// <returns>list of labels formatted as M/D</returns>
    protected List<string> buildLoginLabels(DateTime start_date, int days)
    {
        var labels = new List<string>(days);
        for (var i = 0; i < days; i++)
        {
            var day = start_date.AddDays(i);
            labels.Add($"{day.Month}/{day.Day}");
        }

        return labels;
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
