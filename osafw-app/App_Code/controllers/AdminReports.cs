// Reports Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class AdminReportsController : FwController
{
    public static new int access_level = Users.ACL_MEMBER;
    public static new string route_default_action = FW.ACTION_SHOW;
    private const string CustomReportSaveFields = "icode iname idesc icon access_level sql_template params_json render_options_json status";

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Admin/Reports"; // base url for the controller
        model0 = fw.model<FwReports>();
    }

    /// <summary>
    /// Allows logged-in users to reach the reports module so each report can enforce its own access and RBAC resource.
    /// </summary>
    public override void checkAccess()
    {
        if (fw.userAccessLevel < access_level)
            throw new AuthException("Bad access - Not authorized");
    }

    public FwDict IndexAction()
    {
        FwDict ps = [];
        var customReports = fw.model<FwReports>().listAccessible();
        var isSiteAdmin = fw.model<Users>().isSiteAdmin();
        ps["custom_reports"] = customReports;
        ps["is_site_admin"] = isSiteAdmin;
        ps["is_hardcoded_reports_visible"] = fw.userAccessLevel >= Users.ACL_MANAGER;
        ps["is_custom_reports_block_visible"] = isSiteAdmin || customReports.Count > 0;
        ps["is_custom_reports_visible"] = customReports.Count > 0;

        return ps;
    }

    public void ShowAction(string id)
    {
        var repcode = FwReportsBase.cleanupRepcode(id);
        var filter_session_key = FwReportsBase.filterSessionKey(fw, repcode);

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

        var report = FwReportsBase.createInstance(fw, repcode, list_filter);
        var customReport = report as FwCustomReport;
        try
        {
            report.setFilters(); // set filters data like select/lookups

            if (is_run)
                report.getData();
        }
        catch (Exception ex) when (customReport != null)
        {
            customReport.setExecutionError(ex, fw.model<Users>().isSiteAdmin());
            customReport.format = "html";
            customReport.f["format"] = "html";
        }

        // show or output report according format
        FwDict ps = [];
        ps["return_url"] = return_url;

        report.render(ps);
    }

    /// <summary>
    /// Shows the Site Admin form used to create or modify a database-backed custom report.
    /// </summary>
    /// <param name="id">Report code for edit routes; empty for /Admin/Reports/new.</param>
    /// <returns>Template data for the custom report management form.</returns>
    public FwDict ShowFormAction(string id = "")
    {
        checkSiteAdmin();

        var model = fw.model<FwReports>();
        FwDict item;
        if (string.IsNullOrEmpty(id))
        {
            item = new FwDict
            {
                ["icode"] = model.suggestNextIcode(),
                ["access_level"] = Users.ACL_SITEADMIN,
                ["status"] = FwModel.STATUS_ACTIVE,
                ["render_options_json"] = Utils.jsonEncode(new FwDict
                {
                    ["row_limit"] = FwReports.DEFAULT_ROW_LIMIT,
                    ["preview_limit"] = FwReports.DEFAULT_PREVIEW_LIMIT,
                    ["timeout_seconds"] = FwReports.DEFAULT_TIMEOUT_SECONDS
                }, true)
            };
        }
        else
        {
            item = model.oneManageableByIcode(id).toFwDict();
            if (item.Count == 0)
                throw new NotFoundException();
        }

        Utils.mergeHash(item, reqh("item"));

        var isNew = string.IsNullOrEmpty(id);
        var ps = new FwDict
        {
            ["id"] = item["icode"],
            ["i"] = item,
            ["title"] = isNew ? "Create New Report" : "Edit Report",
            ["is_new"] = isNew,
            ["return_url"] = return_url
        };

        if (reqb("is_preview_ready"))
        {
            var reportCode = item["icode"].toStr();
            if (string.IsNullOrEmpty(reportCode))
                reportCode = "_preview";

            try
            {
                var report = new FwCustomReport(item);
                report.init(fw, reportCode, new FwDict { ["is_preview"] = true });
                report.setFilters();
                report.getData();

                ps["result_headers"] = report.ps["result_headers"];
                ps["result_rows"] = report.ps["result_rows"];
                ps["result_totals"] = report.ps["result_totals"];
                ps["has_result_rows"] = report.ps["has_result_rows"];
                ps["preview_count"] = report.list_count;
                ps["has_preview"] = true;
                ps["has_result_totals"] = report.ps["has_result_totals"];
                ps["is_result_sortable"] = false;
            }
            catch (Exception ex)
            {
                ps["has_preview_error"] = true;
                ps["preview_error_message"] = FwCustomReport.reportErrorMessage(ex);
            }
        }

        return ps;
    }

    /// <summary>
    /// Saves custom report definitions while preserving the legacy editable-report POST path.
    /// </summary>
    /// <param name="id">Report code for edit routes; empty when creating a new custom report.</param>
    /// <returns>Null because save redirects and preview reroutes through the normal form action.</returns>
    public FwDict? SaveAction(string id = "")
    {
        if (!fw.FORM.ContainsKey("item"))
        {
            saveReportChanges(id);
            return null;
        }

        enforcePost();
        checkSiteAdmin();
        route_onerror = FW.ACTION_SHOW_FORM;

        var model = fw.model<FwReports>();
        var item = FormUtils.filter(reqh("item"), CustomReportSaveFields);
        var old = !string.IsNullOrEmpty(id) ? model.oneManageableByIcode(id).toFwDict() : [];
        if (!string.IsNullOrEmpty(id) && old.Count == 0)
            throw new NotFoundException();

        var oldId = old["id"].toInt();

        validateCustomReport(oldId, item);
        model.normalizeForSave(item);

        if (reqb("preview"))
        {
            fw.FORM["item"] = item;
            fw.FORM["is_preview_ready"] = 1;
            if (string.IsNullOrEmpty(id))
                fw.routeRedirect(FW.ACTION_SHOW_FORM);
            else
                fw.routeRedirect(FW.ACTION_SHOW_FORM, [id]);

            return null;
        }

        var savedId = modelAddOrUpdate(oldId, item);
        var saved = model.one(savedId).toFwDict();

        var oldCode = old["icode"].toStr();
        var newCode = saved["icode"].toStr();
        if (!string.IsNullOrEmpty(oldCode) && oldCode != newCode)
            model.retireReportResource(oldCode);

        model.saveReportResource(saved);
        fw.redirect(base_url + "/" + newCode + "/edit");
        return null;
    }

    /// <summary>
    /// Saves changes from legacy editable hardcoded reports.
    /// </summary>
    /// <param name="id">Report code from the route.</param>
    public void SaveReportAction(string id)
    {
        saveReportChanges(id);
    }

    /// <summary>
    /// Shows delete confirmation for a custom report addressed by report code.
    /// </summary>
    /// <param name="id">Report code from the route.</param>
    /// <returns>Template data for delete confirmation.</returns>
    public FwDict ShowDeleteAction(string id)
    {
        checkSiteAdmin();

        var item = fw.model<FwReports>().oneManageableByIcode(id).toFwDict();
        if (item.Count == 0)
            throw new NotFoundException();

        return new FwDict
        {
            ["i"] = item,
            ["id"] = item["icode"],
            ["title"] = "Delete Report"
        };
    }

    /// <summary>
    /// Soft-deletes a custom report and retires its optional report-specific RBAC resource.
    /// </summary>
    /// <param name="id">Report code from the route.</param>
    /// <returns>Null because the action redirects after deletion.</returns>
    public FwDict? DeleteAction(string id)
    {
        checkXSS();
        checkSiteAdmin();

        var model = fw.model<FwReports>();
        var item = model.oneManageableByIcode(id).toFwDict();
        if (item.Count == 0)
            throw new NotFoundException();

        model.delete(item["id"].toInt());
        model.retireReportResource(item["icode"].toStr());
        fw.flash("onedelete", 1);
        fw.redirect(base_url);
        return null;
    }

    /// <summary>
    /// Runs the legacy hardcoded-report save hook for reports that still expose editable state.
    /// </summary>
    /// <param name="id">Report code from the route.</param>
    private void saveReportChanges(string id)
    {
        route_onerror = FW.ACTION_SHOW;

        var repcode = FwReportsBase.cleanupRepcode(id);

        var report = FwReportsBase.createInstance(fw, repcode, reqh("f"));

        if (report.saveChanges())
            fw.redirect(base_url + "/" + repcode + "?is_run=1");
        else
        {
            fw.FORM["is_run"] = 1;
            fw.routeRedirect(FW.ACTION_SHOW, [repcode]);
        }
    }

    /// <summary>
    /// Validates route-level custom-report fields before deeper SQL and JSON normalization runs.
    /// </summary>
    /// <param name="id">Existing report id or 0 for new reports.</param>
    /// <param name="item">Submitted report fields.</param>
    internal void validateCustomReport(int id, FwDict item)
    {
        validateRequired(id, item, "icode iname sql_template");
        item["icode"] = FwReportsBase.cleanupRepcode(item["icode"].toStr().Trim());

        if (!string.IsNullOrEmpty(item["icode"].toStr()))
        {
            if (FwReportsBase.isHardcodedReport(item["icode"].toStr()))
                fw.FormErrors["icode"] = "HARDCODED";
            else if (fw.model<FwReports>().isExistsByField(item["icode"].toStr(), id, "icode"))
                fw.FormErrors["icode"] = "EXISTS";
        }

        validateCheckResult();
    }

    /// <summary>
    /// Ensures only Site Admins can manage or preview unsaved custom report SQL.
    /// </summary>
    private void checkSiteAdmin()
    {
        if (!fw.model<Users>().isSiteAdmin())
            throw new AuthException("Bad access - Site Administrator required");
    }

    /// <summary>
    /// Generates the requested report output and emails it to validated recipients.
    /// </summary>
    /// <param name="id">Report code from the route.</param>
    public void SendEmailAction(string id)
    {
        enforcePost();

        route_onerror = FW.ACTION_SHOW;

        var repcode = FwReportsBase.cleanupRepcode(id);

        var f = reqh("f");
        var to_emails = validateReportRecipients(f["to_emails"].toStr());

        string mail_subject = "Report " + repcode;
        FwDict filenames = [];

        var email_as = f["email_as"].toStr();
        string mail_body;
        if (email_as == "pdf")
        {
            var filepath = FwReportsBase.createFile(fw, repcode, "pdf", f);
            filenames[repcode + ".pdf"] = filepath;
            mail_body = "Report pdf attached";
        }
        else
        {
            var ps = new FwDict {
                { "_layout", fw.config("PAGE_LAYOUT_EMAIL") }
            };
            var html = FwReportsBase.createHtml(fw, repcode, f, ps);
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
