// Signup controller (register new user)
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com


using System.Collections;

namespace osafw;

public class SignupController : FwController
{
    protected Users model = null!;
    public static new string route_default_action = FW.ACTION_INDEX;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<Users>();
        model0 = model;

        required_fields = "email pwd";
        base_url = "/Signup";
        // override layout
        fw.G["PAGE_LAYOUT"] = fw.G["PAGE_LAYOUT_PUBLIC"];

        if (!fw.config("IS_SIGNUP").toBool())
            fw.redirect(fw.config("UNLOGGED_DEFAULT_URL").toStr());
    }

    public void IndexAction()
    {
        fw.routeRedirect(FW.ACTION_SHOW_FORM);
    }

    public FwDict ShowFormAction()
    {
        FwDict ps = [];
        FwDict item = [];

        if (isGet())
        {
        }
        else
            // and merge new values from the form
            Utils.mergeHash(item, reqh("item"));

        ps["i"] = item;
        ps["hide_sidebar"] = true;
        return ps;
    }

    public void SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        var item = reqh("item");
        Validate(item);
        // load old record if necessary
        // var itemdb = model.one(id);

        var itemdb = FormUtils.filter(item, Utils.qw("email pwd fname lname"));

        if (id == 0)
        {
            item["access_level"] = 0;
            item["add_users_id"] = 0;
        }
        id = modelAddOrUpdate(id, itemdb);

        fw.sendEmailTpl(itemdb["email"].toStr(), "signup.txt", itemdb);

        model.doLogin(id);
        fw.redirect(fw.config("LOGGED_DEFAULT_URL").toStr());
    }

    public bool Validate(FwDict item)
    {
        string msg = "";
        bool result = true;
        result &= validateRequired(0, item, Utils.qw(required_fields));
        if (!result)
            msg = "Please fill in all required fields";

        if (result && model.isExists(item["email"].toStr(), 0))
        {
            result = false;
            fw.FormErrors["email"] = "EXISTS";
        }
        if (result && !FormUtils.isEmail(item["email"].toStr()))
        {
            result = false;
            fw.FormErrors["email"] = "EMAIL";
        }

        if (result && item["pwd"].toStr() != item["pwd2"].toStr())
        {
            result = false;
            fw.FormErrors["pwd2"] = "WRONG";
        }

        if (!result)
            throw new UserException(msg);
        return true;
    }
}