// Forgotten Password controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class PasswordController : FwController
{
    protected Users model = new();

    protected int PWD_RESET_EXPIRATION = 60; // minutes

    public override void init(FW fw)
    {
        base.init(fw);
        model.init(fw);
        base_url = "/Password"; // base url for the controller
                                // override layout
        fw.G["PAGE_LAYOUT"] = fw.G["PAGE_LAYOUT_PUBLIC"];
    }

    public Hashtable IndexAction()
    {
        Hashtable ps = new();

        Hashtable item = reqh("item");
        if (isGet())
            // set defaults here
            item = new Hashtable();
        else
        {
        }

        ps["i"] = item;
        ps["hide_sidebar"] = true;
        return ps;
    }

    public void SaveAction()
    {
        route_onerror = FW.ACTION_INDEX; //set route to go if error happens

        string login = reqh("item")["login"].ToString().Trim();

        if (login.Length == 0)
            throw new UserException("Please enter your Email");

        Hashtable user = model.oneByEmail(login);
        if (user.Count == 0 || Utils.f2int(user["status"]) != 0)
            throw new UserException("Not a valid Email");

        model.sendPwdReset(Utils.f2int(user["id"]));

        fw.redirect(base_url + "/(Sent)");
    }

    public Hashtable ResetAction()
    {
        Hashtable ps = new();
        var login = reqs("login");
        var token = reqs("token");
        var user = model.oneByEmail(login);
        if (user.Count == 0 || Utils.f2int(user["status"]) != 0)
            throw new UserException("Not a valid Email");

        if ((string)user["pwd_reset"] == "" || !model.checkPwd(token, (string)user["pwd_reset"])
            || (DateTime.Now - DateTime.Parse((string)user["pwd_reset_time"])).Minutes > PWD_RESET_EXPIRATION)
        {
            fw.flash("error", "Password reset token expired. Use Forgotten password link again.");
            fw.redirect("/Login");
        }

        var item = reqh("item");
        if (isGet())
            // set defaults here
            item = new Hashtable();
        else
        {
        }

        ps["user"] = user;
        ps["token"] = token;
        ps["i"] = item;
        ps["hide_sidebar"] = true;

        return ps;
    }


    public void SaveResetAction()
    {
        route_onerror = "Reset"; //set route to go if error happens

        var item = reqh("item");
        var login = reqs("login");
        var token = reqs("token");
        var user = model.oneByEmail(login);
        if (user.Count == 0 || Utils.f2int(user["status"]) != 0)
            throw new UserException("Not a valid Email");

        if ((string)user["pwd_reset"] == "" || !model.checkPwd(token, (string)user["pwd_reset"])
            || (DateTime.Now - DateTime.Parse((string)user["pwd_reset_time"])).Minutes > PWD_RESET_EXPIRATION)
        {
            fw.flash("error", "Password reset token expired. Use Forgotten password link again.");
            fw.redirect("/Login");
        }

        int id = Utils.f2int(user["id"]);

        ValidateReset(id, item);
        // load old record if necessary
        // Dim itemdb As Hashtable = Users.one(id)

        var itemdb = FormUtils.filter(item, Utils.qw("pwd"));

        itemdb["pwd_reset"] = ""; // also reset token
        model.update(id, itemdb);

        fw.logEvent("chpwd");
        fw.flash("success", "Password updated");

        fw.redirect("/Login");
    }

    public void ValidateReset(int id, Hashtable item)
    {
        bool result = true;
        result &= validateRequired(item, Utils.qw("pwd pwd2"));
        if (!result)
            fw.FormErrors["REQ"] = 1;

        if (result && (string)item["pwd"] != (string)item["pwd2"])
        {
            result = false;
            fw.FormErrors["pwd2"] = "NOTEQUAL";
        }

        this.validateCheckResult();
    }

    public Hashtable SentAction()
    {
        Hashtable ps = new();
        ps["hide_sidebar"] = true;
        return ps;
    }
}