// Login and Registration Page controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Text.RegularExpressions;

namespace osafw;

public class LoginController : FwController
{
    protected Users model = new();

    public override void init(FW fw)
    {
        base.init(fw);
        model.init(fw);

        base_url = "/Login";
        // override layout
        fw.G["PAGE_LAYOUT"] = fw.G["PAGE_LAYOUT_PUBLIC"];
    }

    public Hashtable IndexAction()
    {
        Hashtable ps = new();
        if (fw.isLogged)
            fw.redirect((string)fw.config("LOGGED_DEFAULT_URL"));

        Hashtable item = reqh("item");
        if (isGet())
            // set defaults here
            item = new Hashtable();
        else
        {
        }

        ps["login_mode"] = reqs("mode");
        ps["hide_sidebar"] = true;

        ps["i"] = item;
        ps["err_ctr"] = Utils.toInt(fw.G["err_ctr"]) + 1;
        ps["ERR"] = fw.FormErrors;
        return ps;
    }

    public void SaveAction()
    {
        try
        {
            var item = reqh("item");
            var gourl = reqs("gourl");
            string login = Utils.toStr(item["login"]).Trim();
            string pwd = (string)item["pwdh"];
            // if use field with masked chars - read masked field
            if ((string)item["chpwd"] == "1")
                pwd = (string)item["pwd"];
            pwd = pwd.Trim();

            // for dev config only - login as first admin
            var is_dev_login = false;
            if (Utils.toBool(fw.config("IS_DEV")) && string.IsNullOrEmpty(login) && pwd == "~")
            {
                var dev = db.row(model.table_name, DB.h("status", Users.STATUS_ACTIVE, "access_level", Users.ACL_SITEADMIN), "id");
                login = (string)dev["email"];
                is_dev_login = true;
            }
            else
            {
                // for normal logins - have a delay up to 2s to slow down any brute force attempts
                var ran = new Random();
#pragma warning disable SCS0005 // Weak random generator
                int delay = (int)((ran.NextDouble() * 2 + 0.5) * 1000);
#pragma warning restore SCS0005 // Weak random generator
                System.Threading.Thread.Sleep(delay);
            }

            if (login.Length == 0 || pwd.Length == 0)
            {
                fw.FormErrors["REGISTER"] = true;
                throw new UserException("");
            }

            var user = model.oneByEmail(login);
            if (!is_dev_login)
            {
                if (user.Count == 0 || (string)user["status"] != "0" || !model.checkPwd(pwd, (string)user["pwd"]))
                {
                    fw.logActivity(FwLogTypes.ICODE_USERS_LOGIN_FAIL, FwEntities.ICODE_USERS, 0, login);
                    throw new AuthException("User Authentication Error");
                }

                // check if MFA enabled and redirect to MFA login
                if (!Utils.isEmpty(user["mfa_secret"]))
                {
                    fw.Session("mfa_login_users_id", (string)user["id"]);
                    fw.Session("mfa_login_attempts", "0");
                    fw.Session("mfa_login_time", DateUtils.UnixTimestamp().ToString());
                    fw.Session("mfa_login_remember", Utils.toStr(item["remember"]));
                    fw.Session("mfa_login_gourl", gourl);
                    fw.redirect(base_url + "/(MFA)");
                }

                // no MFA secret for the user here - check if MFA enforced and redirect to setup MFA
                if (Utils.toBool(fw.config("is_mfa_enforced")))
                {
                    fw.Session("mfa_login_users_id", (string)user["id"]);
                    fw.redirect("/My/MFA");
                }
            }

            performLogin(Utils.toInt(user["id"]), Utils.toStr(item["remember"]), gourl);
        }
        catch (ApplicationException ex)
        {
            logger(LogLevel.WARN, ex.Message);
            fw.G["err_ctr"] = reqi("err_ctr") + 1;
            fw.setGlobalError(ex.Message);
            fw.routeRedirect(FW.ACTION_INDEX);
        }
    }

    public void DeleteAction()
    {
        fw.logActivity(FwLogTypes.ICODE_USERS_LOGOFF, FwEntities.ICODE_USERS, fw.userId);
        fw.model<Users>().removePermCookie(fw.userId);
        fw.context.Session.Clear();
        fw.redirect((string)fw.config("UNLOGGED_DEFAULT_URL"));
    }

    public Hashtable MFAAction()
    {
        var users_id = Utils.toInt(fw.Session("mfa_login_users_id"));
        if (users_id == 0)
            fw.redirect(base_url);

        var ps = new Hashtable() {
            { "hide_sidebar" , true},
            { "users_id", users_id }
        };
        return ps;
    }

    public void SaveMFAAction()
    {
        route_onerror = FW.ACTION_INDEX;
        checkXSS();
        var users_id = Utils.toInt(fw.Session("mfa_login_users_id"));
        if (users_id == 0)
            fw.redirect(base_url);

        // check if MFA login expired (more than 5 min after login)
        if (DateUtils.UnixTimestamp() - Utils.toLong(fw.Session("mfa_login_time")) > 60 * 5)
        {
            fw.Session("mfa_login_users_id", "0");
            fw.redirect(base_url);
        }

        // check no more than 10 attempts
        var mfa_login_attempts = Utils.toInt(fw.Session("mfa_login_attempts"));
        if (mfa_login_attempts >= 10)
        {
            fw.Session("mfa_login_users_id", "0");
            fw.redirect(base_url);
        }
        // increase attempts
        fw.Session("mfa_login_attempts", (mfa_login_attempts + 1).ToString());

        var item = reqh("item");
        var mfs_code = item["code"].ToString();

        if (!model.isValidMFA(users_id, mfs_code))
        {
            // invalid MFA code, try recovery codes
            if (!model.checkMFARecovery(users_id, mfs_code))
            {
                fw.flash("error", "Invalid MFA code, try again");
                fw.logActivity(FwLogTypes.ICODE_USERS_LOGIN_FAIL, FwEntities.ICODE_USERS, users_id, "mfa fail");
                fw.redirect(base_url + "/(MFA)");
            }
        }

        // mfa ok - login
        performLogin(users_id, fw.Session("mfa_login_remember"), fw.Session("mfa_login_gourl"));
    }

    private void performLogin(int users_id, string remember, string gourl)
    {
        model.doLogin(users_id);

        // Check is login need to be remembered
        if (!Utils.isEmpty(remember))
            model.createPermCookie(users_id);

        string url;
        if (!string.IsNullOrEmpty(gourl) && !Regex.IsMatch(gourl, "^http", RegexOptions.IgnoreCase))
            url = gourl;
        else
            url = (string)fw.config("LOGGED_DEFAULT_URL");

        fw.redirect(url);
    }
}