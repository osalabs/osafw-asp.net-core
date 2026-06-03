// Reports Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

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
        var requested_format = reqh("f")["format"].toStr().ToLowerInvariant();
        if (Utils.isEmpty(requested_format))
            requested_format = "html";
        list_filter["format"] = requested_format;

        if (requested_format == "json" && !is_send_email)
            is_run = true;

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

    /// <summary>
    /// Generates the requested report output and emails it to validated recipients.
    /// </summary>
    /// <param name="id">Report code from the route.</param>
    public void SendEmailAction(string id)
    {
        enforcePost();

        route_onerror = FW.ACTION_SHOW;

        var repcode = FwReports.cleanupRepcode(id);

        var f = reqh("f");
        var to_emails = validateReportRecipients(f["to_emails"].toStr());

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

    /// <summary>
    /// Validates requested report email recipients before report generation or delivery.
    /// </summary>
    /// <param name="recipients">Raw recipient list submitted from the report email form.</param>
    /// <returns>Semicolon-delimited recipient addresses normalized for <see cref="FW.sendEmail" />.</returns>
    /// <exception cref="UserException">Thrown when the request has no recipients or contains an invalid address.</exception>
    protected virtual string validateReportRecipients(string recipients)
    {
        var emails = Utils.splitEmails(recipients);
        if (emails.Count == 0)
            throw new UserException("Email recipient is required");

        foreach (string email in emails)
            if (!FormUtils.isEmail(email))
                throw new UserException("Invalid email recipient");

        return string.Join(";", emails);
    }
}
