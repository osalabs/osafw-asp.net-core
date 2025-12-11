// Password Reset controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class PasswordResetController : FwController
{
    protected Users model = new();

    protected int PWD_RESET_EXPIRATION = 60; // minutes

    public override void init(FW fw)
    {
        base.init(fw);
        model.init(fw);
        base_url = "/PasswordReset"; // base url for the controller
        // override layout
        fw.G["PAGE_LAYOUT"] = fw.G["PAGE_LAYOUT_PUBLIC"];
    }

    public override void checkAccess()
    {
        //true - allow access to all, including visitors
    }

    // GET /PasswordReset?login=...&token=...
    public FwDict IndexAction()
    {
        FwDict ps = [];
        var login = reqs("login");
        var token = reqs("token");
        var user = model.oneByEmail(login);
        if (user.Count == 0 || user["status"].toInt() != 0)
            throw new UserException("Not a valid Email");

        if (user["pwd_reset"] == "" || !model.checkPwd(token, user["pwd_reset"], Users.PWD_RESET_TOKEN_LEN)
            || (db.Now() - DateTime.Parse(user["pwd_reset_time"])).TotalMinutes > PWD_RESET_EXPIRATION)
        {
            fw.flash("error", "Password reset token expired. Use Forgotten password link again.");
            fw.redirect("/Login");
        }

        var item = reqh("item");
        if (isGet())
            item = [];

        ps["user"] = user;
        ps["token"] = token;
        ps["i"] = item;
        ps["hide_sidebar"] = true;

        return ps;
    }

    // POST /PasswordReset
    public void SaveAction()
    {
        route_onerror = FW.ACTION_INDEX; //set route to go if error happens

        var item = reqh("item");
        var login = reqs("login");
        var token = reqs("token");
        var user = model.oneByEmail(login);
        if (user.Count == 0 || user["status"].toInt() != 0)
            throw new UserException("Not a valid Email");

        if (user["pwd_reset"] == "" || !model.checkPwd(token, user["pwd_reset"], Users.PWD_RESET_TOKEN_LEN)
            || (db.Now() - DateTime.Parse(user["pwd_reset_time"])).TotalMinutes > PWD_RESET_EXPIRATION)
        {
            fw.flash("error", "Password reset token expired. Use Forgotten password link again.");
            fw.redirect("/Login");
        }

        int id = user["id"].toInt();

        Validate(id, item);

        var itemdb = FormUtils.filter(item, Utils.qw("pwd"));

        itemdb["pwd_reset"] = ""; // also reset token
        model.update(id, itemdb);

        fw.logActivity(FwLogTypes.ICODE_USERS_CHPWD, FwEntities.ICODE_USERS);
        fw.flash("success", "Password updated");

        fw.redirect("/Login");
    }

    public void Validate(int id, FwDict item)
    {
        bool result = true;
        result &= validateRequired(id, item, Utils.qw("pwd pwd2"));
        if (!result)
            fw.FormErrors["REQ"] = 1;

        if (result && item["pwd"].toStr() != item["pwd2"].toStr())
        {
            result = false;
            fw.FormErrors["pwd2"] = "NOTEQUAL";
        }

        this.validateCheckResult();
    }
}
