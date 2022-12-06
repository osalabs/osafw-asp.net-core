// Login and Registration Page controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;

namespace osafw;

public class LoginController : FwController
{
    protected Users model = new();

    public override void init(FW fw)
    {
        base.init(fw);
        model.init(fw);
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
        ps["err_ctr"] = Utils.f2int(fw.G["err_ctr"]) + 1;
        ps["ERR"] = fw.FormErrors;
        return ps;
    }

    public void SaveAction()
    {            
        try
        {
            var item = reqh("item");
            var gourl = reqs("gourl");
            string login = Utils.f2str(item["login"]).Trim();
            string pwd = (string)item["pwdh"];
            // if use field with masked chars - read masked field
            if ((string)item["chpwd"] == "1")
                pwd = (string)item["pwd"];
            pwd = Strings.Trim(pwd);

            // for dev config only - login as first admin
            var is_dev_login = false;
            if (Utils.f2bool(fw.config("IS_DEV")) && string.IsNullOrEmpty(login) && pwd == "~")
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
                    fw.logEvent("login_fail", 0, 0, login);
                    throw new AuthException("User Authentication Error");
                }
            }

            if (model.doLogin(Utils.f2int(user["id"])))
            {
                // Check is login need to be remembered
                if (item.ContainsKey("remember"))
                {
                    model.createPermCookie(Utils.f2int(user["id"]));
                }
            }

            if (!string.IsNullOrEmpty(gourl) && !Regex.IsMatch(gourl, "^http", RegexOptions.IgnoreCase))
                fw.redirect(gourl);
            else
                fw.redirect((string)fw.config("LOGGED_DEFAULT_URL"));
        }
        catch (ApplicationException ex)
        {

            fw.logEvent("logoff", fw.userId);

            fw.model<Users>().removePermCookie(fw.userId);
            fw.context.Session.Clear();
            fw.redirect((string)fw.config("UNLOGGED_DEFAULT_URL"));
        }
    }

    public void DeleteAction()
    {
        fw.logEvent("logoff", fw.userId);

        fw.context.Session.Clear();
        fw.redirect((string)fw.config("UNLOGGED_DEFAULT_URL"));
    }
}