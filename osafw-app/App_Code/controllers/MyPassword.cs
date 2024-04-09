// MyPassword controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class MyPasswordController : FwController
{
    public static new int access_level = Users.ACL_MEMBER;

    protected Users model = new();

    public override void init(FW fw)
    {
        base.init(fw);
        model.init(fw);

        base_url = "/My/Password";

        is_readonly = false;//allow update my stuff
    }

    public void IndexAction()
    {
        fw.redirect(base_url + "/new");
    }

    public Hashtable ShowFormAction()
    {
        if (reqs("result") == "record_updated")
            fw.G["green_msg"] = "Login/Password has been changed";

        Hashtable ps = new();
        Hashtable item = reqh("item");
        int id = fw.userId;

        if (isGet())
        {
            if (id > 0)
                item = model.one(id);
            else
                // set defaults here
                item = new Hashtable();
        }
        else
        {
            // read from db
            Hashtable itemdb = model.one(id);
            // and merge new values from the form
            Utils.mergeHash(itemdb, item);
            item = itemdb;
        }

        ps["id"] = id;
        ps["i"] = item;
        ps["ERR"] = fw.FormErrors;
        return ps;
    }

    public void SaveAction()
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        int id = fw.userId;

        Validate(id, reqh("item"));
        // load old record if necessary
        // Dim itemdb As Hashtable = Users.one(id)

        var itemdb = FormUtils.filter(reqh("item"), Utils.qw("email pwd"));
        itemdb["pwd"] = itemdb["pwd"].ToString().Trim();

        if (id > 0)
        {
            model.update(id, itemdb);

            fw.logActivity(FwLogTypes.ICODE_USERS_CHPWD, FwEntities.ICODE_USERS);
            fw.flash("record_updated", 1);
        }

        afterSave(true, id);
    }

    public void Validate(int id, Hashtable item)
    {
        bool result = true;
        result &= validateRequired(id, item, Utils.qw("email old_pwd pwd pwd2"));
        if (!result)
            fw.FormErrors["REQ"] = 1;

        if (result && model.isExists(item["email"], id))
        {
            result = false;
            fw.FormErrors["email"] = "EXISTS";
        }
        if (result && !FormUtils.isEmail((string)item["email"]))
        {
            result = false;
            fw.FormErrors["email"] = "EMAIL";
        }

        if (result && model.cleanPwd((string)item["pwd"]) != model.cleanPwd((string)item["pwd2"]))
        {
            result = false;
            fw.FormErrors["pwd2"] = "NOTEQUAL";
        }

        // uncomment if project requires good password strength
        // If result AndAlso item.ContainsKey("pwd") AndAlso model.scorePwd(item["pwd"]) <= 60 Then
        // result = False
        // fw.FERR["pwd") ] "BAD"
        // End If

        if (result)
        {
            Hashtable itemdb = model.one(id);
            if (!fw.model<Users>().checkPwd((string)item["old_pwd"], (string)itemdb["pwd"]))
            {
                result = false;
                fw.FormErrors["old_pwd"] = "WRONG";
            }
        }

        this.validateCheckResult();
    }

}