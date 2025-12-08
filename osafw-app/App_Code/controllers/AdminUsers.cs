// Admin Users controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

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

    public override Hashtable setPS(Hashtable ps = null)
    {
        ps = base.setPS(ps);
        ps["is_roles"] = model.isRoles();
        return ps;
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        var ps = base.ShowFormAction(id);
        Hashtable item = (Hashtable)ps["i"];
        ps["att"] = fw.model<Att>().one(item["att_id"]);

        ps["is_roles"] = model.isRoles();
        ps["roles_link"] = model.listLinkedRoles(id);

        return ps;
    }

    public override Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in Controller.save_fields");

        if (reqb("refresh"))
        {
            fw.routeRedirect(FW.ACTION_SHOW_FORM, [id]);
            return null;
        }

        Hashtable item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        item["email"] = item["ehack"]; // just because Chrome autofills fields too agressively

        Validate(id, item);
        // load old record if necessary
        // var itemOld = model0.one(id);

        Hashtable itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());

        itemdb["pwd"] = itemdb["pwd"].ToString().Trim();
        if (Utils.isEmpty(itemdb["pwd"]))
            itemdb.Remove("pwd");

        id = this.modelAddOrUpdate(id, itemdb);

        model.updateLinkedRoles(id, reqh("roles_link"));

        if (fw.userId == id)
            model.reloadSession(id);

        return this.afterSave(success, id, is_new);
    }

    public override void Validate(int id, Hashtable item)
    {
        bool result = true;
        result &= validateRequired(id, item, Utils.qw(required_fields));
        if (!result)
            fw.FormErrors["REQ"] = 1;

        if (result && model.isExists(item["email"], id))
        {
            result = false;
            fw.FormErrors["ehack"] = "EXISTS";
        }
        if (result && !FormUtils.isEmail((string)item["email"]))
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

        fw.redirect((string)fw.config("LOGGED_DEFAULT_URL"));
    }

    public Hashtable SendPwdAction(int id)
    {
        Hashtable ps = [];

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
            if (row["pwd"].ToString().Substring(0, 2) == "$2")
                continue; // already hashed
            var hashed = model.hashPwd((string)row["pwd"]);
            db.update(model.table_name, new Hashtable() { { "pwd", hashed } }, new Hashtable() { { "id", row["id"] } });
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