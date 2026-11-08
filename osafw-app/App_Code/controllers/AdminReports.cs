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
        route_onerror = string.Empty;
        var repcode = FwReportsBase.cleanupRepcode(id);
        var filter_session_key = FwReportsBase.filterSessionKey(fw, repcode);

        if (reqb("doreset"))
        {
            fw.Session(filter_session_key, "");
            fw.redirect(base_url + "/" + repcode);
        }

        var is_send_email = reqb("send_email");
        var is_run = (reqb("dofilter") || reqb("is_run")) && !is_send_email;

        var fwcron_id = reqi("fwcron_id");
        FwCron.Row? task = null;
        FwDict? saved = null;
        if (fwcron_id > 0)
        {
            checkSiteAdmin();
            task = getReportTask(fwcron_id);
            list_filter = new FwDict(reqh("f"));
        }
        else
        {
            initFilter(filter_session_key);
        }

        // get format directly form request as we don't need to remember format
        var requested_format = reqh("f")["format"].toStr().ToLowerInvariant();
        if (Utils.isEmpty(requested_format))
            requested_format = "html";
        list_filter["format"] = requested_format;

        if (requested_format == "json" && !is_send_email)
            is_run = true;

        var report = task == null
            ? FwReportsBase.createInstance(fw, repcode, list_filter)
            : loadScheduledReport(task, repcode, list_filter, out saved);

        var customReport = report as FwCustomReport;
        var is_review_required = false;
        try
        {
            if (saved == null)
            {
                report.setFilters();
            }
            else if (!Utils.isEmpty(fw.G["report_schedule_error"]))
            {
                report.showScheduledFormFilters(list_filter);
            }
            else if (reqb("dofilter"))
            {
                report.captureScheduledFilters(list_filter, true);
            }
            else
            {
                try
                {
                    report.applyScheduledFilters(saved);
                }
                catch (UserException)
                {
                    // A changed filter contract needs explicit review and a new submitted snapshot.
                    is_review_required = true;
                    report.showScheduledFormFilters(list_filter, is_frozen: false);
                }
            }

            report.f["format"] = requested_format;
            if (is_run && !is_review_required)
                report.getData();
        }
        catch (Exception ex) when (customReport != null && ex is not AuthException)
        {
            customReport.setExecutionError(ex, fw.model<Users>().isSiteAdmin());
            customReport.format = "html";
            customReport.f["format"] = "html";
        }

        // show or output report according format
        FwDict ps = [];
        ps["return_url"] = return_url;
        ps["report_email_status"] = fw.flash("report_email_status");
        ps["report_email_error"] = fw.G["report_email_error"];
        if (report.format == "html" && report.isSchedulingSupported && fw.model<Users>().isSiteAdmin())
            addSchedulePageState(ps, report, task, is_review_required);

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
                addFormError("icode", "HARDCODED");
            else if (fw.model<FwReports>().isExistsByField(item["icode"].toStr(), id, "icode"))
                addFormError("icode", "EXISTS");
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

    /// <summary>Creates one report-email schedule from the current submitted report filters.</summary>
    public void ScheduleEmailAction(string id)
    {
        enforcePost();
        checkSiteAdmin();
        checkReadOnly();
        route_onerror = FW.ACTION_SHOW;

        var report = FwReportsBase.createScheduledInstance(fw, FwReportsBase.cleanupRepcode(id), []);
        var item = reqh("item");
        var itemdb = FormUtils.filter(item, "iname cron timezone start_date_utc end_date_utc status");
        validateRequired(0, itemdb, "iname");
        validateCheckResult();
        var name = itemdb["iname"].toStr().Trim();
        itemdb["iname"] = name.Length > 255 ? name[..255] : name;

        fw.model<FwCron>().prepareSchedule(itemdb);
        var reports = fw.model<FwReports>();
        var payload = reports.captureSchedulePayload(report, reqh("f"), item);
        itemdb["payload"] = FwReports.encodeSchedulePayload(payload);

        var fwcron_id = reports.addSchedule(report.schedulingCode, itemdb);
        saveScheduleFeedback(report, fwcron_id, (DateTime?)itemdb["next_run_utc"]);
        fw.redirect(base_url + "/" + report.schedulingCode, is_exception: false);
    }

    /// <summary>Replaces only the selected report schedule's filter snapshot after validating current input.</summary>
    public void UpdateScheduleFiltersAction(string id)
    {
        enforcePost();
        checkSiteAdmin();
        checkReadOnly();
        route_onerror = FW.ACTION_SHOW;

        var task = getReportTask(reqi("fwcron_id"));
        var report = loadScheduledReport(task, FwReportsBase.cleanupRepcode(id), [], out var saved);

        saved["params"] = report.captureScheduledFilters(reqh("f"), true);
        saved.Remove("sortby");
        saved.Remove("sortdir");
        report.saveScheduledSort(saved);

        if (!fw.model<FwCron>().updateDefinition(task.id, task, new FwDict { ["payload"] = FwReports.encodeSchedulePayload(saved) }))
            throw new UserException("REPORT_SCHEDULE_CHANGED");

        saveScheduleFeedback(report, task.id, task.next_run_utc);
        fw.redirect(base_url + "/" + report.schedulingCode + "?fwcron_id=" + task.id, is_exception: false);
    }

    /// <summary>Preserves the submitted schedule form across the normal report error reroute.</summary>
    public override FwDict? actionError(Exception? ex, object[] args)
    {
        if (route_onerror == FW.ACTION_SHOW && !fw.isJsonExpected() && ex is UserException and not NotFoundException)
        {
            if (fw.route.action is "ScheduleEmail" or "UpdateScheduleFilters")
            {
                fw.G["report_schedule_error"] = ex is ValidationException ? "REPORT_INPUT_INVALID" : ex.Message;
                return base.actionError(new ValidationException(), args);
            }

            if (fw.route.action == "SendEmail" && ex is not ValidationException)
            {
                fw.G["report_email_error"] = ex.Message;
                return base.actionError(new ValidationException(), args);
            }
        }

        if (fw.route.action == FW.ACTION_SHOW && !fw.isJsonExpected() && ex is UserException)
        {
            var message = FormUtils.selectTplName("/admin/reports/common/schedule_messages.sel", ex.Message);
            if (message.Length > 0)
                ex = new UserException(fw.parsePageInstance().langMap(message));
        }

        return base.actionError(ex, args);
    }

    /// <summary>Restores a schedule's input conventions and verifies its original report before interpreting filters.</summary>
    private FwReportsBase loadScheduledReport(FwCron.Row task, string code, FwDict filters, out FwDict saved)
    {
        var reports = fw.model<FwReports>();
        saved = reports.parseSchedulePayload(task.icode, task.payload.toStr());
        reports.applyScheduleInputContext(saved);
        var report = FwReportsBase.createScheduledInstance(fw, code, filters);
        FwReports.checkScheduleTarget(report, FwReports.reportCode(task.icode), saved);
        return report;
    }

    private FwCron.Row getReportTask(int id)
    {
        var task = fw.model<FwCron>().oneT(id);
        if (task == null || task.status == FwModel.STATUS_DELETED || FwReports.reportCode(task.icode).Length == 0)
            throw new NotFoundException();

        return task;
    }

    private void saveScheduleFeedback(FwReportsBase report, int fwcron_id, DateTime? next_run)
    {
        fw.flash("report_schedule", new FwDict
        {
            ["code"] = report.schedulingCode,
            ["fwcron_id"] = fwcron_id,
            ["next_run"] = next_run.HasValue ? DateUtils.Date2SQL(next_run.Value, is_include_time: true) : null
        });
    }

    private void addSchedulePageState(FwDict ps, FwReportsBase report, FwCron.Row? task, bool is_review_required)
    {
        ps["is_schedule_available"] = true;
        ps["schedule_report_code"] = report.schedulingCode;
        ps["schedule_search"] = FwReports.REPORT_PREFIX + report.schedulingCode + ":";
        var error = fw.G["report_schedule_error"].toStr();
        var is_cron_error = error.StartsWith("CRON_", StringComparison.Ordinal);
        ps["is_schedule_error"] = error.Length > 0;
        ps["schedule_error"] = is_cron_error ? "" : error;
        ps["schedule_cron_error"] = is_cron_error ? error : "";

        if (task != null)
        {
            ps["fwcron_id"] = task.id;
            ps["schedule_review_required"] = is_review_required;
        }
        else
        {
            var item = new FwDict
            {
                ["iname"] = report.schedulingTitle,
                ["recipients"] = fw.model<Users>().one(fw.userId)["email"],
                ["format"] = "pdf",
                ["status"] = FwModel.STATUS_ACTIVE
            };
            if (!isGet())
                Utils.mergeHash(item, FormUtils.filter(reqh("item"), "iname recipients format cron timezone start_date_utc end_date_utc status"));

            ps["is_schedule_initial"] = isGet();
            ps["schedule_item"] = item;
            ps["schedule_run_as"] = fw.Session("user_name");
            ps["cron_editor"] = fw.model<FwCron>().cronEditor(item, "report-cron", is_external_preview: true);
        }

        if (fw.flash("report_schedule") is FwDict saved && saved["code"].toStr() == report.schedulingCode)
        {
            ps["schedule_success"] = true;
            ps["saved_fwcron_id"] = saved["fwcron_id"];
            ps["schedule_next_run"] = saved["next_run"];
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

        var repcode = FwReportsBase.cleanupRepcode(id);

        var f = reqh("f");
        var to_emails = validateReportRecipients(f["to_emails"].toStr());

        var email_as = f["email_as"].toStr();
        if (email_as == "pdf")
            f["format"] = "pdf";

        var fwcron_id = reqi("fwcron_id");
        FwReportsBase report;
        if (fwcron_id > 0)
        {
            checkSiteAdmin();
            report = loadScheduledReport(getReportTask(fwcron_id), repcode, f, out _);
            report.captureScheduledFilters(f, is_frozen: true);
        }
        else
        {
            report = FwReportsBase.createInstance(fw, repcode, f);
            report.setFilters();
        }
        report.getData();

        var res = report.sendReportEmail(to_emails, email_as);
        fw.flash("report_email_status", res ? "REPORT_SENT" : "REPORT_SEND_FAILED");

        fw.redirect(base_url + "/" + repcode + (fwcron_id > 0 ? "?fwcron_id=" + fwcron_id : ""));
    }

    /// <summary>
    /// Validates requested report email recipients before report generation or delivery.
    /// </summary>
    /// <param name="recipients">Raw recipient list submitted from the report email form.</param>
    /// <returns>Semicolon-delimited recipient addresses normalized for <see cref="FW.sendEmail" />.</returns>
    /// <exception cref="ValidationException">Thrown when the request contains no valid recipients.</exception>
    protected virtual string validateReportRecipients(string recipients)
    {
        var normalized = FwReportsBase.normalizeRecipients(recipients);
        validateRequired(0, new FwDict { ["to_emails"] = normalized }, "to_emails");
        validateCheckResult();
        return normalized;
    }
}
