// Send Email Admin controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class AdminSendEmailController : FwAdminController
{
    public static new int access_level = Users.ACL_ADMIN;

    protected Users model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<Users>();
        model0 = model;

        base_url = "/Admin/SendEmail";
        required_fields = "from to subject";
        save_fields = "from to subject body host port username password";
        save_fields_checkboxes = "is_ssl|0";

        var mailSettings = fw.config("mail") as FwDict ?? [];
        form_new_defaults = new FwDict
        {
            ["from"] = fw.config("mail_from").toStr(),
            ["host"] = mailSettings["host"].toStr(),
            ["port"] = mailSettings["port"].toInt(),
            ["username"] = mailSettings["username"].toStr(),
            ["password"] = mailSettings["password"].toStr(),
            ["is_ssl"] = mailSettings["is_ssl"].toBool()
        };
    }

    public override FwDict? IndexAction()
    {
        fw.redirect(base_url + "/new");
        return null;
    }

    public override FwDict ShowFormAction(int id = 0)
    {
        var ps = base.ShowFormAction(id)!;
        ps["test_email"] = fw.Session("login"); // in test mode send to current user
        return ps;
    }

    public override FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in save_fields");

        if (reqb("refresh"))
        {
            fw.routeRedirect(FW.ACTION_SHOW_FORM, [id]);
            return null;
        }

        FwDict item = reqh("item");

        Validate(id, item);
        // load old record if necessary
        // var itemOld = model.one(id);

        FwDict itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());

        var options = new FwDict
        {
            ["smtp"] = FormUtils.filter(itemdb, "host port is_ssl username password")
        };
        var is_sent = fw.sendEmail(itemdb["from"].toStr(), itemdb["to"].toStr(), itemdb["subject"].toStr(), itemdb["body"].toStr(), null, null, "", options);

        var ps = new FwDict
        {
            ["is_sent"] = is_sent,
            ["last_error_send_email"] = fw.last_error_send_email
        };

        return ps;
    }

    public override void Validate(int id, FwDict item)
    {
        bool result = this.validateRequired(id, item, this.required_fields);

        //if (result && !SomeOtherValidation())
        //{
        //    fw.FERR["other field name"] = "HINT_ERR_CODE";
        //}

        this.validateCheckResult();
    }

}
