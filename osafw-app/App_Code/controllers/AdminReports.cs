// Reports Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class AdminReportsController : FwController
{
    public static new int access_level = Users.ACL_MANAGER;
    public static new string route_default_action = FW.ACTION_SHOW;

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Admin/Reports"; // base url for the controller
    }

    public FwDict IndexAction()
    {
        FwDict ps = [];

        return ps;
    }

    public void ShowAction(string id)
    {
        var repcode = FwReports.cleanupRepcode(id);
        var filter_session_key = FwReports.filterSessionKey(fw, repcode);

        if (reqb("doreset"))
        {
            fw.Session(filter_session_key, "");
            fw.redirect(base_url + "/" + repcode);
        }

        var is_send_email = reqb("send_email");
        var is_run = (reqb("dofilter") || reqb("is_run")) && !is_send_email;

        // report filters (options)
        initFilter(filter_session_key);

        // get format directly form request as we don't need to remember format
        list_filter["format"] = reqh("f")["format"];
        if (Utils.isEmpty(list_filter["format"]))
            list_filter["format"] = "html";

        var report = FwReports.createInstance(fw, repcode, list_filter);
        report.setFilters(); // set filters data like select/lookups

        if (is_run)
            report.getData();

        // show or output report according format
        FwDict ps = [];
        ps["return_url"] = return_url;

        report.render(ps);
    }

    // save changes from editable reports
    public void SaveAction(string id)
    {
        route_onerror = FW.ACTION_SHOW;

        var repcode = FwReports.cleanupRepcode(id);

        var report = FwReports.createInstance(fw, repcode, reqh("f"));

        if (report.saveChanges())
            fw.redirect(base_url + "/" + repcode + "?is_run=1");
        else
        {
            fw.FORM["is_run"] = 1;
            fw.routeRedirect(FW.ACTION_SHOW, [repcode]);
        }
    }

    public void SendEmailAction(string id)
    {
        route_onerror = FW.ACTION_SHOW;

        var repcode = FwReports.cleanupRepcode(id);

        var f = reqh("f");
        var to_emails = f["to_emails"].toStr();

        string mail_subject = "Report " + repcode;
        FwDict filenames = [];

        var email_as = f["email_as"].toStr();
        string mail_body;
        if (email_as == "pdf")
        {
            var filepath = FwReports.createFile(fw, repcode, "pdf", f);
            filenames[repcode + ".pdf"] = filepath;
            mail_body = "Report pdf attached";
        }
        else
        {
            var ps = new FwDict {
                { "_layout", fw.config("PAGE_LAYOUT_EMAIL") }
            };
            var html = FwReports.createHtml(fw, repcode, f, ps);
            mail_body = html;
        }

        //sending from logged user
        var user = fw.model<Users>().one(fw.userId);
        var res = fw.sendEmail(user["email"], to_emails, mail_subject, mail_body, filenames);
        if (res)
            fw.flash("success", "Report sent");
        else
            fw.flash("error", "Error sending email:" + fw.last_error_send_email);

        fw.redirect(base_url + "/" + repcode);
    }
}