// Reports Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

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
        model0 = fw.model<FwReportsModel>();
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
        var customReports = fw.model<FwReportsModel>().listAccessible();
        var isSiteAdmin = fw.model<Users>().isSiteAdmin();
        ps["custom_reports"] = customReports;
        ps["is_site_admin"] = isSiteAdmin;
        ps["can_view_hardcoded_reports"] = fw.userAccessLevel >= Users.ACL_MANAGER;
        ps["has_custom_reports_block"] = isSiteAdmin || customReports.Count > 0;
        ps["has_custom_reports"] = customReports.Count > 0;

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

    /// <summary>
    /// Shows the Site Admin form used to create or modify a database-backed custom report.
    /// </summary>
    /// <param name="id">Report code for edit routes; empty for /Admin/Reports/new.</param>
    /// <returns>Template data for the custom report management form.</returns>
    public FwDict ShowFormAction(string id = "")
    {
        checkSiteAdmin();

        var model = fw.model<FwReportsModel>();
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
                    ["row_limit"] = FwReportsModel.DEFAULT_ROW_LIMIT,
                    ["preview_limit"] = FwReportsModel.DEFAULT_PREVIEW_LIMIT,
                    ["timeout_seconds"] = FwReportsModel.DEFAULT_TIMEOUT_SECONDS
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
        return buildShowFormPs(id, item);
    }

    /// <summary>
    /// Saves custom report definitions while preserving the legacy editable-report POST path.
    /// </summary>
    /// <param name="id">Report code for edit routes; empty when creating a new custom report.</param>
    /// <returns>Template data for preview requests; otherwise redirects.</returns>
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

        var model = fw.model<FwReportsModel>();
        var item = FormUtils.filter(reqh("item"), CustomReportSaveFields);
        var old = !string.IsNullOrEmpty(id) ? model.oneManageableByIcode(id).toFwDict() : [];
        if (!string.IsNullOrEmpty(id) && old.Count == 0)
            throw new NotFoundException();

        var oldId = old["id"].toInt();

        validateCustomReport(oldId, item);
        model.normalizeForSave(item, oldId);

        if (reqb("preview"))
        {
            item["is_preview"] = true;
            var ps = buildShowFormPs(id, item);
            addPreview(ps, item);
            return ps;
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

        var item = fw.model<FwReportsModel>().oneManageableByIcode(id).toFwDict();
        if (item.Count == 0)
            throw new NotFoundException();

        return new FwDict
        {
            ["i"] = item,
            ["id"] = item["icode"],
            ["base_url"] = base_url,
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

        var model = fw.model<FwReportsModel>();
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
    /// Builds the shared form template state for create, edit, validation, and preview responses.
    /// </summary>
    /// <param name="id">Current route report code; empty for a new report.</param>
    /// <param name="item">Submitted or stored report fields to display.</param>
    /// <returns>Template data for the custom-report management form.</returns>
    private FwDict buildShowFormPs(string id, FwDict item)
    {
        var title = string.IsNullOrEmpty(id) ? "Create New Report" : "Edit Report";
        var formAction = string.IsNullOrEmpty(id) ? base_url + "/new" : base_url + "/" + FwReportsModel.cleanupIcode(id) + "/edit";

        return new FwDict
        {
            ["_basedir"] = "/admin/reports/showform",
            ["id"] = item["icode"],
            ["i"] = item,
            ["title"] = title,
            ["base_url"] = base_url,
            ["form_action"] = formAction,
            ["is_new"] = string.IsNullOrEmpty(id),
            ["return_url"] = return_url,
            ["custom_report_help"] = buildCustomReportHelp(),
            ["custom_report_help_sql"] = buildCustomReportHelpSql(),
            ["custom_report_help_params"] = buildCustomReportHelpParams()
        };
    }

    /// <summary>
    /// Validates route-level custom-report fields before deeper SQL and JSON normalization runs.
    /// </summary>
    /// <param name="id">Existing report id or 0 for new reports.</param>
    /// <param name="item">Submitted report fields.</param>
    private void validateCustomReport(int id, FwDict item)
    {
        validateRequired(id, item, "icode iname sql_template");
        item["icode"] = FwReportsModel.cleanupIcode(item["icode"].toStr());

        if (!string.IsNullOrEmpty(item["icode"].toStr()) && fw.model<FwReportsModel>().isExistsByField(item["icode"].toStr(), id, "icode"))
            fw.FormErrors["icode"] = "EXISTS";

        validateCheckResult();
    }

    /// <summary>
    /// Executes an unsaved custom report definition with preview limits and attaches rows to the form response.
    /// </summary>
    /// <param name="ps">Form template data to augment.</param>
    /// <param name="item">Normalized unsaved report fields.</param>
    private void addPreview(FwDict ps, FwDict item)
    {
        var reportCode = item["icode"].toStr();
        if (string.IsNullOrEmpty(reportCode))
            reportCode = "_preview";

        var report = new FwCustomReport(item);
        report.init(fw, reportCode, new FwDict { ["is_preview"] = true });
        report.setFilters();
        report.getData();

        ps["preview_headers"] = report.ps["result_headers"];
        ps["preview_rows"] = report.ps["result_rows"];
        ps["preview_count"] = report.list_count;
        ps["has_preview"] = true;
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
    /// Provides compact inline authoring help for Site Admins on the custom report form.
    /// </summary>
    /// <returns>Plain text instructions shown beside SQL and parameter metadata fields.</returns>
    private static string buildCustomReportHelp()
    {
        return """
Use one SELECT or CTE query. Use @param placeholders for filters, then define optional parameter metadata as JSON.
Supported parameter types: text, int, number, date, datetime, lookup.
Lookup source may be users, fwentities, log_types, static options, or sql:SELECT id, iname FROM table.
""";
    }

    /// <summary>
    /// Provides a copyable SQL example that runs on the default SQL Server schema.
    /// </summary>
    /// <returns>Example custom report SQL.</returns>
    private static string buildCustomReportHelpSql()
    {
        return """
SELECT id, fname, lname, email, access_level, add_time
FROM users
WHERE (@access_level IS NULL OR access_level = @access_level)
  AND (@s IS NULL OR email LIKE '%' + @s + '%' OR fname LIKE '%' + @s + '%' OR lname LIKE '%' + @s + '%')
ORDER BY add_time DESC
""";
    }

    /// <summary>
    /// Provides copyable parameter metadata matching the SQL help example.
    /// </summary>
    /// <returns>Example params_json content.</returns>
    private static string buildCustomReportHelpParams()
    {
        return """
[
  {"name":"access_level","label":"Access Level","type":"int"},
  {"name":"s","label":"Search","type":"text"}
]
""";
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
