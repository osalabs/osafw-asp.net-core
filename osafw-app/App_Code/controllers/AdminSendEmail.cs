// Send Email Admin controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class AdminSendEmailController : FwAdminController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    protected Users model = null!;

    public override void init(FW fw)
    {
        FwSettingsProtection.markSensitiveRequest(fw);
        base.init(fw);
        model = fw.model<Users>();
        model0 = model;

        base_url = "/Admin/SendEmail";
        required_fields = "from to subject";
        save_fields = "from to subject body host port username";
        save_fields_checkboxes = "is_ssl|0";

        var settings = fw.model<Settings>();
        form_new_defaults = new FwDict
        {
            ["from"] = settings.read("mail_from"),
            ["host"] = settings.read("mail.host"),
            ["port"] = settings.readInt("mail.port", 587),
            ["username"] = settings.read("mail.username"),
            ["password"] = "",
            ["is_ssl"] = settings.readBool("mail.is_ssl", true)
        };
    }

    public override FwDict? IndexAction()
    {
        requireSiteAdmin();
        fw.redirect(base_url + "/new");
        return null;
    }

    public override FwDict ShowFormAction(int id = 0)
    {
        requireSiteAdmin();
        var ps = base.ShowFormAction(id)!;
        if (ps["i"] is FwDict item)
            item.Remove("password");
        ps["test_email"] = fw.resolveTestEmailRecipient();
        return ps;
    }

    public override FwDict? SaveAction(int id = 0)
    {
        enforcePost();
        checkReadOnly();
        requireSiteAdmin();

        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in save_fields");

        FwDict item = reqh("item");
        var submittedPassword = item["password"].toStr();
        item.Remove("password");

        if (isRefreshOnlyRequest())
        {
            fw.routeRedirect(FW.ACTION_SHOW_FORM, [id]);
            return null;
        }

        Validate(id, item);
        // load old record if necessary
        // var itemOld = model.one(id);

        FwDict itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());

        var smtp = FormUtils.filter(itemdb, "host port is_ssl username");
        if (!string.IsNullOrEmpty(submittedPassword))
            smtp["password"] = submittedPassword;

        var options = new FwDict
        {
            ["smtp"] = smtp
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

    private void requireSiteAdmin()
    {
        if (fw.userAccessLevel < Users.ACL_SITEADMIN)
            throw new AuthException();
    }

}
