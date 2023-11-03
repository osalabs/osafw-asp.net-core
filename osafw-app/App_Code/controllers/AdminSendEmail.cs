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

    protected Users model;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<Users>();
        model0 = model;

        base_url = "/Admin/SendEmail";
        required_fields = "from to subject";
        save_fields = "from to subject body host port username password";
        save_fields_checkboxes = "is_ssl|0";

        Hashtable mailSettings = (Hashtable)fw.config("mail");
        form_new_defaults = new Hashtable
        {
            ["from"] = Utils.f2str(fw.config("mail_from")),
            ["host"] = Utils.f2str(mailSettings["host"]),
            ["port"] = Utils.f2int(mailSettings["port"]),
            ["username"] = Utils.f2str(mailSettings["username"]),
            ["password"] = Utils.f2str(mailSettings["password"]),
            ["is_ssl"] = Utils.f2int(mailSettings["is_ssl"]),
        };
    }

    public override Hashtable IndexAction()
    {
        fw.redirect(base_url + "/new");
        return null;
    }

    public override Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in save_fields");

        if (reqi("refresh") == 1)
        {
            fw.routeRedirect(FW.ACTION_SHOW_FORM, new object[] { id });
            return null;
        }

        Hashtable item = reqh("item");

        Validate(id, item);
        // load old record if necessary
        // Dim item_old As Hashtable = model.one(id)

        Hashtable itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes);

        var options = new Hashtable
        {
            ["smtp"] = FormUtils.filter(itemdb, "host port is_ssl username password")
        };
        var is_sent = fw.sendEmail(itemdb["from"].ToString(), itemdb["to"].ToString(), itemdb["subject"].ToString(), itemdb["body"].ToString(), null, null, "", options);

        var ps = new Hashtable
        {
            ["is_sent"] = is_sent,
            ["last_error_send_email"] = fw.last_error_send_email
        };

        return ps;
    }

    public override void Validate(int id, Hashtable item)
    {
        bool result = this.validateRequired(item, this.required_fields);

        //if (result && !SomeOtherValidation())
        //{
        //    fw.FERR["other field name"] = "HINT_ERR_CODE";
        //}

        this.validateCheckResult();
    }

}