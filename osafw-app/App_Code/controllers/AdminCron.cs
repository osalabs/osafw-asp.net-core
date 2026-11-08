using Cronos;
using System;
using System.Globalization;
using System.Collections;
using System.Data.Common;
using System.Linq;

namespace osafw;

public class AdminCronController : FwDynamicController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    private const string DEFAULT_CRON = "0 7 * * *";
    private const string BOUND_FORMAT = "yyyy-MM-dd'T'HH:mm:ss";
    private FwCron model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Admin/Cron";
        loadControllerConfig();
        model = model0 as FwCron ?? throw new FwConfigUndefinedModelException();
        db = model.getDB();
        is_activity_logs = true;
        is_userlists = true;
        list_fields = "id, icode, iname, idesc, cron, timezone, status, start_date_utc, end_date_utc, next_run_utc, "
            + "run_requested_utc, run_token, last_started_utc, last_finished_utc, last_result, add_time, add_users_id, upd_time, upd_users_id";
    }

    public override void checkAccess()
    {
        checkSiteAdmin();
        access_actions_to_permissions["Preview"] = Permissions.PERMISSION_VIEW;
        access_actions_to_permissions["RunNow"] = Permissions.PERMISSION_EDIT;
        access_actions_to_permissions["Pause"] = Permissions.PERMISSION_EDIT;
        access_actions_to_permissions["Resume"] = Permissions.PERMISSION_EDIT;
        access_actions_to_permissions["Recover"] = Permissions.PERMISSION_EDIT;
        base.checkAccess();
    }

    public override FwDict initFilter(string? session_key = null)
    {
        var filter = base.initFilter(session_key);
        if (fw.route.action == FW.ACTION_SHOW && Utils.isEmpty(filter["tab_activity"]))
            filter["tab_activity"] = FwActivityLogs.TAB_ALL;
        return filter;
    }

    public override void getListRows()
    {
        base.getListRows();
        foreach (var row in list_rows)
            row["task"] = taskDisplay(row);
    }

    public override FwDict IndexAction()
    {
        var ps = base.IndexAction();
        ps["is_runner_disabled"] = !isRunnerEnabled();
        return ps;
    }

    public override FwDict? ShowAction(int id = 0)
    {
        var task = taskOrFail(id);
        var ps = base.ShowAction(id);
        if (ps == null)
            return null;

        var row = (FwDict)ps["i"]!;
        ps["task"] = taskDisplay(row);
        ps["is_readonly"] = is_readonly || !string.IsNullOrEmpty(task.run_token);
        ps["is_hide_delete"] = ps["is_readonly"];
        ps["is_runner_disabled"] = !isRunnerEnabled();
        if (!is_readonly)
            ps["run_token"] = task.run_token;
        var result = FwCron.parseResult(task.last_result ?? "");
        if (result["data"] is { } details)
            ps["result_details"] = Utils.jsonEncode(details, true);

        addReportDisplay(ps, task);
        return ps;
    }

    public override FwDict? ShowFormAction(int id = 0)
    {
        var task = id > 0 ? taskOrFail(id) : null;
        var ps = base.ShowFormAction(id);
        if (ps == null)
            return null;

        var item = (FwDict)ps["i"]!;
        if (task == null)
        {
            ps["job_codes"] = new FwList(model.legacyCodes()
                .Where(code => !code.StartsWith(FwCron.REPORT_PREFIX, StringComparison.OrdinalIgnoreCase))
                .Select(code => new FwDict
                {
                    ["id"] = code,
                    ["iname"] = code
                }));
        }
        else
        {
            item["icode"] = task.icode;
            ps["is_readonly"] = is_readonly || !string.IsNullOrEmpty(task.run_token);
        }

        var posted = isGet() ? new FwDict() : reqh("item");
        item["start_date_utc"] = posted.ContainsKey("start_date_utc") ? posted["start_date_utc"] : boundInput(fw, task?.start_date_utc);
        item["end_date_utc"] = posted.ContainsKey("end_date_utc") ? posted["end_date_utc"] : boundInput(fw, task?.end_date_utc);
        ps["is_paused"] = item["status"].toStr() == FwModel.STATUS_INACTIVE.ToString();
        ps["is_completed"] = task?.status == FwCron.STATUS_COMPLETED;
        ps["cron_editor"] = cronEditor(fw, item, "task-cron", taskId: id, isExternalPreview: true);
        if (task != null)
        {
            var payload = addReportDisplay(ps, task);
            if (payload != null)
            {
                ps["recipients"] = posted["recipients"] ?? payload["recipients"];
                ps["delivery_format"] = posted["format"] ?? payload["format"];
                ps["run_as_users_id"] = posted["run_as_users_id"] ?? payload["run_as_users_id"];
                var admins = db.array(fw.model<Users>().table_name, DB.h("status", FwModel.STATUS_ACTIVE,
                    "access_level", db.opGE(Users.ACL_SITEADMIN)), "fname, lname", new StrList { "id", "fname", "lname" });
                ps["run_as_users"] = new FwList(admins.Select(user => new FwDict
                {
                    ["id"] = user["id"],
                    ["iname"] = (user["fname"] + " " + user["lname"]).Trim()
                }));
            }
        }

        return ps;
    }

    public override FwDict? SaveAction(int id = 0)
    {
        enforceMutation();
        route_onerror = FW.ACTION_SHOW_FORM;
        var observed = id > 0 ? taskOrFail(id) : null;
        if (!string.IsNullOrEmpty(observed?.run_token))
            throw new UserException("Wait for the current run before editing this task.");

        var submitted = reqh("item");
        var fields = FormUtils.filter(submitted, save_fields);
        var name = fields["iname"].toStr().Trim();
        if (name.Length == 0 || name.Length > 255)
            throw new UserException("Enter a task name of up to 255 characters.");

        fields["iname"] = name;
        prepareSchedule(fw, fields, observed);
        if (observed == null)
        {
            var code = submitted["icode"].toStr();
            if (code.StartsWith(FwCron.REPORT_PREFIX, StringComparison.OrdinalIgnoreCase)
                || !model.legacyCodes().Contains(code, StringComparer.Ordinal))
                throw new UserException("Choose a supported application job code.");

            if (model.isExistsByField(code, 0, "icode"))
                throw new UserException("A task already uses this job code.");

            fields["icode"] = code;
            id = model.add(fields);
            model.logEvent(id, "Created scheduled task");
            return afterSave(true, id, is_new: true);
        }

        if (FwCron.reportCode(observed.icode).Length > 0)
        {
            var reports = fw.model<FwScheduledReports>();
            var payload = reports.payload(observed.icode, observed.payload ?? "");
            var updated = reports.validateDelivery(payload, submitted["recipients"].toStr(),
                submitted["format"].toStr(), submitted["run_as_users_id"].toInt());
            fields["payload"] = FwScheduledReports.encodePayload(updated);
        }

        if (!model.saveDefinition(id, observed, fields))
            throw new UserException("The task changed or started running. Reload and try again.");

        return afterSave(true, id);
    }

    public override void ShowDeleteAction(int id)
    {
        checkSiteAdmin();
        if (!string.IsNullOrEmpty(taskOrFail(id).run_token))
            throw new UserException("Wait for the current run before deleting this task.");

        var ps = new FwDict
        {
            ["id"] = id,
            ["i"] = modelOneOrFail(id)
        };
        fw.parser("/admin/cron/showdelete", ps);
    }

    public override FwDict? DeleteAction(int id)
    {
        enforceMutation();
        taskOrFail(id);
        route_onerror = FW.ACTION_SHOW;
        if (!model.deleteTask(id))
            throw new UserException("The task changed or started running. Reload and try again.");

        fw.flash("onedelete", 1);
        return afterSave(true);
    }

    // Keep standard bulk/list workflows while routing task deletion through its concurrency guard.
    public override FwDict? SaveMultiAction()
    {
        // The standard list form POSTs with the framework's PUT method override.
        if (!string.Equals(fw.request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            throw new AuthException("POST required");

        if (string.Equals(fw.route.method, "PUT", StringComparison.OrdinalIgnoreCase))
            checkXSS();
        else
            enforcePost();

        checkSiteAdmin();
        checkReadOnly();
        return base.SaveMultiAction();
    }

    protected override int saveMultiRows(ICollection keys, bool is_delete, int user_lists_id, int remove_user_lists_id)
    {
        foreach (string key in keys)
            taskOrFail(key.toInt());

        if (!is_delete)
            return base.saveMultiRows(keys, false, user_lists_id, remove_user_lists_id);

        var deleted = 0;
        foreach (string key in keys)
        {
            if (model.deleteTask(key.toInt()))
                deleted++;
        }
        if (deleted != keys.Count)
            fw.flash("error", "Running or changed tasks were not deleted. Reload and try again.");
        return deleted;
    }

    public override FwDict? RestoreDeletedAction(int id) => throw new NotFoundException();
    public override FwDict SaveAttFilesAction(int id) => throw new NotFoundException();

    public void PreviewAction()
    {
        checkSiteAdmin();
        try
        {
            var taskId = reqi("task_id");
            var observed = taskId > 0 ? taskOrFail(taskId) : null;
            var (cron, zone, start, end) = readSchedule(fw, reqh("item"), observed);
            var occurrences = new FwList();
            var next = DateTime.UtcNow;
            var viewerZone = findZone(fw.userTimezone);
            for (var i = 0; i < 3; i++)
            {
                var occurrence = FwCron.calculateNextRun(cron, next, end, zone, start);
                if (!occurrence.HasValue)
                    break;

                next = occurrence.Value;
                occurrences.Add(new FwDict
                {
                    ["recurrence"] = zonedTime(fw, next, zone, includeZone: false),
                    ["recurrence_title"] = zonedTime(fw, next, zone),
                    ["local"] = zonedTime(fw, next, viewerZone, includeZone: false),
                    ["local_title"] = zonedTime(fw, next, viewerZone),
                    ["utc"] = utcTime(next)
                });
            }

            fw.parserJson(new FwDict { ["occurrences"] = occurrences });
        }
        catch (UserException ex)
        {
            fw.response.StatusCode = 400;
            fw.parserJson(new FwDict { ["error"] = ex.Message });
        }
    }

    public void RunNowAction(int id)
    {
        enforceMutation();
        if (!isRunnerEnabled())
            throw new UserException("The task runner is disabled in this build.");

        var task = taskOrFail(id);
        if (task.status == FwCron.STATUS_COMPLETED || task.end_date_utc <= DateTime.UtcNow)
            throw new UserException("Revise or resume this schedule before requesting a run.");

        if (model.requestRun(id))
            finishAction(id, "Queued");
        else
        {
            task = taskOrFail(id);
            if (!task.run_requested_utc.HasValue && string.IsNullOrEmpty(task.run_token))
                throw new UserException("The task changed. Reload and try again.");

            finishAction(id, "Already queued/running");
        }
    }

    public void PauseAction(int id)
    {
        enforceMutation();
        taskOrFail(id);
        if (!model.pause(id))
            throw new UserException("The task could not be paused. Reload and try again.");

        finishAction(id, "Paused");
    }

    public void ResumeAction(int id)
    {
        enforceMutation();
        if (!model.resume(taskOrFail(id)))
            throw new UserException("The task could not be resumed. Check its schedule and current state.");

        finishAction(id, "Resumed");
    }

    public void RecoverAction(int id)
    {
        enforceMutation();
        taskOrFail(id);
        var token = reqs("run_token");
        if (!reqb("confirm_recovery") || token.Length == 0 || !model.recover(id, token))
            throw new UserException("Confirm the worker has stopped and reload the task if its state changed.");

        finishAction(id, "Interrupted run recorded as uncertain. The task is paused.");
    }

    /// <summary>Builds template data for the shared editor. The caller supplies a unique prefix for each instance.</summary>
    internal static FwDict cronEditor(FW fw, FwDict values, string prefix,
        string cronField = "item[cron]", string timezoneField = "item[timezone]", int taskId = 0, bool isExternalPreview = false)
    {
        var timezone = values["timezone"].toStr(fw.userTimezone);
        var choices = TimeZoneInfo.GetSystemTimeZones().Select(zone => zone.Id)
            .Append(DateUtils.TZ_UTC).Append(timezone).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
        var zones = new FwList();
        foreach (var zone in choices)
            zones.Add(new FwDict
            {
                ["id"] = zone,
                ["iname"] = zone
            });

        return new FwDict
        {
            ["prefix"] = prefix,
            ["is_external_preview"] = isExternalPreview,
            ["task_id"] = taskId,
            ["cron_field"] = cronField,
            ["timezone_field"] = timezoneField,
            ["cron"] = values["cron"].toStr(DEFAULT_CRON),
            ["timezone"] = timezone,
            ["zones"] = zones,
            ["viewer_zone"] = fw.userTimezone,
            ["preview_url"] = fw.config("ROOT_URL").toStr() + "/Admin/Cron/(Preview)"
        };
    }

    /// <summary>Validates an editable schedule and resolves posted viewer-local bounds into UTC.</summary>
    internal static void prepareSchedule(FW fw, FwDict fields, TFwCron? observed = null)
    {
        var (cron, zone, start, end) = readSchedule(fw, fields, observed);
        var requestedStatus = fields["status"].toStr("0");
        if (requestedStatus != "0" && requestedStatus != "10" &&
            !(requestedStatus == "20" && observed?.status == FwCron.STATUS_COMPLETED))
            throw new UserException("Choose a valid schedule status.");

        var status = requestedStatus.toInt();
        var is_changed = observed == null || observed.cron != cron || observed.timezone != zone.Id
            || observed.start_date_utc != start || observed.end_date_utc != end;
        var is_recalculate = is_changed || (status == FwModel.STATUS_ACTIVE && observed?.status != status);
        var next = is_recalculate ? FwCron.calculateNextRun(cron, DateTime.UtcNow, end, zone, start) : observed?.next_run_utc;
        if (is_recalculate && status == FwModel.STATUS_ACTIVE && !next.HasValue)
            throw new UserException("No future occurrence fits this schedule and its start/end bounds.");

        fields["cron"] = cron;
        fields["timezone"] = zone.Id;
        fields["start_date_utc"] = start;
        fields["end_date_utc"] = end;
        fields["next_run_utc"] = next;
        fields["status"] = status;
    }

    private static (string cron, TimeZoneInfo zone, DateTime? start, DateTime? end) readSchedule(
        FW fw, FwDict values, TFwCron? observed = null)
    {
        var cron = values["cron"].toStr().Trim();
        if (cron.Length > 255 || cron.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length != 5
            || !CronExpression.TryParse(cron, out _))
            throw new UserException("Enter a valid five-field cron expression.");

        var zone = findZone(values["timezone"].toStr().Trim());
        var start = readBound(fw, values["start_date_utc"], observed?.start_date_utc);
        var end = readBound(fw, values["end_date_utc"], observed?.end_date_utc);
        if (start.HasValue && end.HasValue && start >= end)
            throw new UserException("The end must be later than the start.");

        return (cron, zone, start, end);
    }

    private static TimeZoneInfo findZone(string id)
    {
        if (id.Length > 0 && id.Length <= 64 && !id.Equals("Auto", StringComparison.OrdinalIgnoreCase)
            && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            return zone;

        throw new UserException("Choose a valid recurrence timezone.");
    }

    private static DateTime? readBound(FW fw, object? value, DateTime? observed)
    {
        var raw = value.toStr().Trim();
        if (raw.Length == 0)
            return null;

        var zone = findZone(fw.userTimezone);
        if (!DateTime.TryParseExact(raw, [BOUND_FORMAT, "yyyy-MM-dd'T'HH:mm"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var local) || zone.IsInvalidTime(local))
            throw new UserException("Enter a valid start/end date and time in your timezone.");

        // Keep the original instant when the unchanged clock time falls in a daylight-saving fold.
        if (observed.HasValue && boundInput(fw, observed) == local.ToString(BOUND_FORMAT, CultureInfo.InvariantCulture))
            return observed;

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    private static string boundInput(FW fw, DateTime? value) =>
        value.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(value.Value, findZone(fw.userTimezone))
            .ToString(BOUND_FORMAT, CultureInfo.InvariantCulture) : "";

    private FwDict taskDisplay(FwDict row)
    {
        var task = new FwDict(row);
        var status = row["status"].toInt();
        var is_running = !string.IsNullOrEmpty(row["run_token"].toStr());
        var is_queued = !Utils.isEmpty(row["run_requested_utc"]);
        var is_expired = !Utils.isEmpty(row["end_date_utc"]) && row["end_date_utc"].toDate() <= DateTime.UtcNow;
        task["state"] = status switch
        {
            FwModel.STATUS_ACTIVE => "Active",
            FwModel.STATUS_INACTIVE => "Paused",
            FwCron.STATUS_COMPLETED => "Completed",
            _ => "Unavailable"
        };
        task["is_running"] = is_running;
        task["is_queued"] = is_queued;
        var is_runnable = !is_expired && !is_running && !is_queued
            && (status == FwModel.STATUS_ACTIVE || status == FwModel.STATUS_INACTIVE);
        task["can_run"] = !is_readonly && isRunnerEnabled() && is_runnable;
        task["is_run_disabled"] = !isRunnerEnabled() && is_runnable;
        task["can_pause"] = !is_readonly && (status == FwModel.STATUS_ACTIVE || is_queued);
        task["can_resume"] = !is_readonly && !is_running && status != FwModel.STATUS_ACTIVE;
        task["can_edit"] = !is_readonly && !is_running;
        task["report_code"] = FwCron.reportCode(row["icode"].toStr());
        task["report_path"] = Uri.EscapeDataString(task["report_code"].toStr());
        task["schedule_description"] = describeSchedule(row["cron"].toStr());

        var resultJson = row["last_result"].toStr();
        var result = FwCron.parseResult(resultJson);
        task["result_status"] = result["status"];
        task["result_message"] = result.Count > 0 ? result["message"] : resultJson.Length > 0 ? "Result details unavailable" : "";
        addTimestamp(task, row, "next_run_utc", "next");
        if (!Utils.isEmpty(row["next_run_utc"]))
        {
            var next = row["next_run_utc"].toDate();
            var scheduleZone = findZone(row["timezone"].toStr());
            task["next_time"] = zonedTime(fw, next, scheduleZone, includeZone: false);
            task["next_utc"] = scheduleZone.Id == DateUtils.TZ_UTC ? "" : utcTime(next);
        }
        addTimestamp(task, row, "start_date_utc", "start");
        addTimestamp(task, row, "end_date_utc", "end");
        addTimestamp(task, row, "last_started_utc", "last");
        addTimestamp(task, row, "last_finished_utc", "finished");
        return task;
    }

    private FwDict? addReportDisplay(FwDict ps, TFwCron task)
    {
        var report = FwCron.reportCode(task.icode);
        if (report.Length == 0)
            return null;

        ps["report_code"] = report;
        ps["report_path"] = Uri.EscapeDataString(report);
        try
        {
            var payload = fw.model<FwScheduledReports>().payload(task.icode, task.payload ?? "");
            var user = fw.model<Users>().iname(payload["run_as_users_id"]);
            var parameters = (FwList)payload["params"]!;
            ps["report_params"] = new FwList(parameters.Select(parameter => new FwDict
            {
                ["label"] = parameter["name"],
                ["value"] = parameter["value"]
            }));
            try
            {
                var filters = new FwDict();
                foreach (var parameter in parameters)
                    filters[parameter["name"].toStr()] = parameter["value"];
                var instance = FwReportsBase.createInstance(fw, report, filters);
                FwScheduledReports.checkTarget(instance, report, payload);
                ps["report_params"] = instance.describeScheduledFilters(parameters);
                ps["can_edit_report_filters"] = true;
            }
            catch (Exception ex) when (ex is UserException or AuthException or DbException or ApplicationException)
            {
                ps["is_report_unavailable"] = true;
            }
            ps["report_run_as"] = user.Length > 0 ? user : "Unavailable";
            ps["report_recipients"] = payload["recipients"];
            ps["report_format"] = payload["format"];
            return payload;
        }
        catch (UserException)
        {
            ps["is_report_payload_unavailable"] = true;
            return null;
        }
    }

    private void addTimestamp(FwDict task, FwDict row, string field, string prefix)
    {
        if (Utils.isEmpty(row[field]))
            return;

        var value = DateTime.SpecifyKind(row[field].toDate(), DateTimeKind.Utc);
        var zone = findZone(fw.userTimezone);
        task[prefix + "_local"] = zonedTime(fw, value, zone);
        if (zone.Id != DateUtils.TZ_UTC)
            task[prefix + "_utc"] = utcTime(value);
    }

    private static string zonedTime(FW fw, DateTime value, TimeZoneInfo zone, bool includeZone = true)
    {
        var local = TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)), zone);
        var format = DateUtils.mapDateFormat(fw.userDateFormat) + " " + DateUtils.mapTimeFormat(fw.userTimeFormat);
        return local.ToString(format, CultureInfo.InvariantCulture)
            + (includeZone ? " " + zone.Id + " (" + local.ToString("zzz") + ")" : "");
    }

    private static string utcTime(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private string describeSchedule(string cron)
    {
        var fields = cron.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5 || fields[3] != "*"
            || !int.TryParse(fields[0], out var minute) || minute < 0 || minute > 59
            || !int.TryParse(fields[1], out var hour) || hour < 0 || hour > 23)
            return cron;

        var time = new DateTime(2000, 1, 1, hour, minute, 0).ToString(DateUtils.mapTimeFormat(fw.userTimeFormat), CultureInfo.InvariantCulture);
        if (fields[2] == "*" && fields[4] == "*")
            return "Daily at " + time;
        if (fields[2] == "*" && fields[4] == "1-5")
            return "Weekdays at " + time;
        if (fields[2] == "*" && fields[4].Split(',').All(day => day.Length == 1 && day[0] >= '0' && day[0] <= '6'))
        {
            var days = fields[4].Split(',').Select(day => CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedDayNames[day[0] - '0']);
            return string.Join(", ", days) + " at " + time;
        }
        if (fields[4] == "*" && int.TryParse(fields[2], out var date) && date >= 1 && date <= 31)
            return "Day " + date + " at " + time + " each month";

        return cron;
    }

    private void finishAction(int id, string message)
    {
        fw.flash("success", message);
        fw.redirect(base_url + "/" + id, is_exception: false);
    }

    private TFwCron taskOrFail(int id)
    {
        var task = model.getTask(id);
        if (task == null || task.status == FwModel.STATUS_DELETED)
            throw new NotFoundException();

        return task;
    }

    private void checkSiteAdmin()
    {
        if (fw.userAccessLevel < Users.ACL_SITEADMIN)
            throw new AuthException("Site Administrator required");
    }

    private void enforceMutation()
    {
        enforcePost();
        checkSiteAdmin();
        checkReadOnly();
    }

    private static bool isRunnerEnabled()
    {
#if isFwCronService
        return true;
#else
        return false;
#endif
    }
}
