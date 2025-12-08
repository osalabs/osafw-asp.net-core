// My Settings controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class MySettingsController : FwController
{
    public static new int access_level = Users.ACL_MEMBER;

    protected Users model = new();

    public override void init(FW fw)
    {
        base.init(fw);
        model.init(fw);
        required_fields = "email"; // default required fields, space-separated
        base_url = "/My/Settings"; // base url for the controller

        save_fields = "email fname lname address1 address2 city state zip phone lang ui_theme ui_mode date_format time_format timezone";

        is_readonly = false;//allow update my stuff
    }

    public void IndexAction()
    {
        fw.redirect(base_url + "/new");
    }

    public Hashtable ShowFormAction()
    {
        Hashtable ps = [];
        Hashtable item = reqh("item");
        var id = fw.userId;

        if (isGet())
            item = model.one(id);
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

        return ps;
    }

    public void SaveAction()
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        var item = reqh("item");
        var id = fw.userId;

        Validate(id, item);
        // load old record if necessary
        // var itemOld = model.one(id);

        Hashtable itemdb = FormUtils.filter(item, save_fields);

        model.update(id, itemdb);
        fw.flash("record_updated", 1);

        model.reloadSession();

        afterSave(true, id);
    }

    public void Validate(int id, Hashtable item)
    {
        bool result = true;
        result &= validateRequired(id, item, Utils.qw(required_fields));
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

        //if (result && !SomeOtherValidation())
        //{
        //    fw.FERR["other field name"] = "HINT_ERR_CODE";
        //}

        this.validateCheckResult();
    }
}