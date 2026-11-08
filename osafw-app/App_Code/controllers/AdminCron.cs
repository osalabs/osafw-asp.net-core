using System;
using System.Globalization;
using System.Collections;
using System.Data.Common;
using System.Linq;

namespace osafw;

public class AdminCronController : FwDynamicController
{
    public static new int access_level = Users.ACL_SITEADMIN;

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
        list_fields = "id, icode, iname, idesc, cron, timezone, status, start_date_utc, end_date_utc, next_run_utc, run_requested_utc, run_token, last_started_utc, last_finished_utc, last_result, add_time, add_users_id, upd_time, upd_users_id";
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
        ps["is_runner_disabled"] = !FwCron.isRunnerEnabled();
        return ps;
    }

    public override FwDict? ShowAction(int id = 0)
    {
        var task = taskOrFail(id, is_include_deleted: true);
        var ps = base.ShowAction(id);
        if (ps == null)
            return null;

        var row = (FwDict)ps["i"]!;
        ps["task"] = taskDisplay(row);
        var is_deleted = task.status == FwModel.STATUS_DELETED;
        var is_idle_writable = !is_readonly && task.run_token == null;
        ps["is_readonly"] = !is_idle_writable || is_deleted;
        ps["is_hide_delete"] = ps["is_readonly"];
        ps["can_restore"] = is_idle_writable && is_deleted && !FwReports.isReportTaskCode(task.icode);
        ps["can_purge_report"] = is_idle_writable && is_deleted && FwReports.isReportTaskCode(task.icode);
        ps["is_runner_disabled"] = !FwCron.isRunnerEnabled();
        if (!is_readonly && !is_deleted)
            ps["run_token"] = task.run_token;
        var result = FwCron.parseResult(task.last_result ?? "");
        if (result["data"] is { } details)
            ps["result_details"] = Utils.jsonEncode(details, true);

        if (ps["activity_rows"] is FwList activityRows)
        {
            foreach (var activity in activityRows)
            {
                if (activity["log_type"] is DBRow logType && logType["icode"].toStr() == FwLogTypes.ICODE_CRON_EVENT)
                    activity["idesc"] = cronMessage(activity["idesc"].toStr());
            }
        }

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
                .Where(code => !FwReports.isReportTaskCode(code))
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
        item["start_date_utc"] = posted.ContainsKey("start_date_utc") ? posted["start_date_utc"] : model.boundInput(task?.start_date_utc);
        item["end_date_utc"] = posted.ContainsKey("end_date_utc") ? posted["end_date_utc"] : model.boundInput(task?.end_date_utc);
        ps["is_completed"] = task?.status == FwCron.STATUS_COMPLETED;
        ps["cron_editor"] = model.cronEditor(item, "task-cron", fwcron_id: id, is_external_preview: true);
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

    public override void Validate(int id, FwDict item)
    {
        base.Validate(id, item);
        if (id == 0)
        {
            var code = item["icode"].toStr();
            if (FwReports.isReportTaskCode(code)
                || !model.legacyCodes().Contains(code, StringComparer.Ordinal))
                addFormError("icode", "CRON_JOB", cronMessage("CRON_JOB"));
            else if (model.isExistsByField(code, 0, "icode"))
                addFormError("icode", "CRON_JOB_EXISTS", cronMessage("CRON_JOB_EXISTS"));
        }

        validateCheckResult();
    }

    public override FwDict? SaveAction(int id = 0)
    {
        enforcePost();
        checkReadOnly();
        if (id < 0)
            throw new NotFoundException();

        route_onerror = FW.ACTION_SHOW_FORM;

        var observed = id > 0 ? taskOrFail(id) : null;
        if (!string.IsNullOrEmpty(observed?.run_token))
            throw new UserException(cronMessage("CRON_EDIT_RUNNING"));

        var item = reqh("item");
        item["iname"] = item["iname"].toStr().Trim();
        var itemdb = prepareSaveFields(id, item);
        var name = itemdb["iname"].toStr();
        itemdb["iname"] = name.Length > 255 ? name[..255] : name;

        try
        {
            model.prepareSchedule(itemdb, observed);
            if (observed != null && FwReports.reportCode(observed.icode).Length > 0)
            {
                var reports = fw.model<FwReports>();
                var payload = reports.parseSchedulePayload(observed.icode, observed.payload ?? "");
                var updated = reports.validateScheduleDelivery(payload, item["recipients"].toStr(),
                    item["format"].toStr(), item["run_as_users_id"].toInt());
                itemdb["payload"] = FwReports.encodeSchedulePayload(updated);
            }
        }
        catch (UserException ex)
        {
            throw new UserException(cronMessage(ex.Message));
        }

        if (observed == null)
        {
            itemdb["icode"] = item["icode"];
            id = model.add(itemdb);
            fw.flash("record_added", 1);
            return afterSave(true, id, is_new: true);
        }

        var is_updated = model.updateDefinition(id, observed, itemdb);
        if (is_updated)
        {
            fw.flash("record_updated", 1);
        }
        else
        {
            fw.setGlobalError(cronMessage("CRON_CHANGED"));
            if (fw.isJsonExpected())
                fw.response.StatusCode = 400;
        }

        return afterSave(is_updated, id);
    }

    public override void ShowDeleteAction(int id)
    {
        checkReadOnly();
        var task = taskOrFail(id, is_include_deleted: true);
        if (task.status == FwModel.STATUS_DELETED && !FwReports.isReportTaskCode(task.icode))
            throw new NotFoundException();
        if (task.run_token != null)
            throw new UserException(cronMessage("CRON_CHANGED"));

        base.ShowDeleteAction(id);
    }

    public override FwDict? DeleteAction(int id)
    {
        enforceMethod("POST", "DELETE");
        checkReadOnly();
        var task = taskOrFail(id, is_include_deleted: true);
        route_onerror = FW.ACTION_SHOW;

        if (!model.deleteTask(id))
        {
            finishAction(id, "CRON_CHANGED", "error");
            return null;
        }

        fw.flash("onedelete", 1);
        return afterSave(true, location: FwReports.isReportTaskCode(task.icode) ? base_url : "");
    }

    protected override int saveMultiRows(ICollection keys, bool is_delete, int user_lists_id, int remove_user_lists_id)
    {
        foreach (string key in keys)
            taskOrFail(key.toInt(), is_include_deleted: is_delete);

        if (!is_delete)
            return base.saveMultiRows(keys, false, user_lists_id, remove_user_lists_id);

        var deleted = 0;
        foreach (string key in keys)
        {
            if (model.deleteTask(key.toInt()))
                deleted++;
        }
        if (deleted != keys.Count)
            fw.flash("error", cronMessage("CRON_BULK_CHANGED"));
        return deleted;
    }

    public override FwDict? RestoreDeletedAction(int id)
    {
        enforcePost();
        checkReadOnly();
        taskOrFail(id, is_include_deleted: true);
        route_onerror = FW.ACTION_SHOW;

        if (!model.restoreTask(id))
        {
            finishAction(id, "CRON_CHANGED", "error");
            return null;
        }

        fw.flash("record_updated", 1);
        return afterSave(true, id, location: base_url + "/" + id);
    }

    public override FwDict SaveAttFilesAction(int id) => throw new NotFoundException();

    public void PreviewAction()
    {
        try
        {
            var fwcron_id = reqi("fwcron_id");
            var observed = fwcron_id > 0 ? taskOrFail(fwcron_id) : null;
            var (cron, zone, start, end) = model.readSchedule(reqh("item"), observed);
            var occurrences = new FwList();
            var next = DateTime.UtcNow;
            var viewer_zone = FwCron.findZone(fw.userTimezone);
            for (var i = 0; i < 3; i++)
            {
                var occurrence = FwCron.calculateNextRun(cron, next, end, zone, start);
                if (!occurrence.HasValue)
                    break;

                next = occurrence.Value;
                occurrences.Add(new FwDict
                {
                    ["recurrence"] = zonedTime(next, zone, is_include_zone: false),
                    ["recurrence_title"] = zonedTime(next, zone),
                    ["local"] = zonedTime(next, viewer_zone, is_include_zone: false),
                    ["local_title"] = zonedTime(next, viewer_zone),
                    ["utc"] = utcTime(next)
                });
            }

            fw.parserJson(new FwDict { ["occurrences"] = occurrences });
        }
        catch (UserException ex)
        {
            fw.response.StatusCode = 400;
            fw.parserJson(new FwDict { ["error"] = cronMessage(ex.Message) });
        }
    }

    public void RunNowAction(int id)
    {
        enforcePost();
        checkReadOnly();
        if (!FwCron.isRunnerEnabled())
            throw new UserException(cronMessage("CRON_RUNNER_DISABLED"));

        var task = taskOrFail(id);
        if (task.status == FwCron.STATUS_COMPLETED || task.end_date_utc <= DateTime.UtcNow)
            throw new UserException(cronMessage("CRON_RESUME_BEFORE_RUN"));

        if (model.requestRun(id))
            finishAction(id, "CRON_QUEUED");
        else
        {
            // The request may have lost to another request or the worker after its cached read.
            model.removeCache(id);
            task = taskOrFail(id);
            if (task.run_requested_utc.HasValue || !string.IsNullOrEmpty(task.run_token))
                finishAction(id, "CRON_ALREADY_QUEUED", "info");
            else
                finishAction(id, "CRON_CHANGED", "error");
        }
    }

    public void PauseAction(int id)
    {
        enforcePost();
        checkReadOnly();
        taskOrFail(id);
        if (model.pause(id))
            finishAction(id, "CRON_PAUSED");
        else
            finishAction(id, "CRON_CHANGED", "error");
    }

    public void ResumeAction(int id)
    {
        enforcePost();
        checkReadOnly();
        if (model.resume(taskOrFail(id)))
            finishAction(id, "CRON_RESUMED");
        else
            finishAction(id, "CRON_RESUME_FAILED", "error");
    }

    public void RecoverAction(int id)
    {
        enforcePost();
        checkReadOnly();
        taskOrFail(id);
        var token = reqs("run_token");
        if (!reqb("confirm_recovery") || token.Length == 0)
            throw new UserException(cronMessage("CRON_RECOVER_FAILED"));

        if (model.recover(id, token))
            finishAction(id, "CRON_RECOVERED");
        else
            finishAction(id, "CRON_RECOVER_FAILED", "error");
    }

    private FwDict taskDisplay(FwDict row)
    {
        var task = new FwDict(row);
        var status = row["status"].toInt();
        var is_deleted = status == FwModel.STATUS_DELETED;
        var is_running = !string.IsNullOrEmpty(row["run_token"].toStr());
        var is_queued = !Utils.isEmpty(row["run_requested_utc"]);
        var is_expired = !Utils.isEmpty(row["end_date_utc"]) && row["end_date_utc"].toDate() <= DateTime.UtcNow;
        task["is_running"] = is_running;
        task["is_queued"] = is_queued;
        var is_runnable = !is_expired && !is_running && !is_queued
            && (status == FwModel.STATUS_ACTIVE || status == FwModel.STATUS_INACTIVE);
        task["can_run"] = !is_readonly && FwCron.isRunnerEnabled() && is_runnable;
        task["is_run_disabled"] = !FwCron.isRunnerEnabled() && is_runnable;
        task["can_pause"] = !is_readonly && !is_deleted && (status == FwModel.STATUS_ACTIVE || is_queued);
        task["can_resume"] = !is_readonly && !is_running && status is FwModel.STATUS_INACTIVE or FwCron.STATUS_COMPLETED;
        task["can_edit"] = !is_readonly && !is_running && !is_deleted;
        task["report_code"] = FwReports.reportCode(row["icode"].toStr());
        setScheduleDescription(task);

        var result_json = row["last_result"].toStr();
        var result = FwCron.parseResult(result_json);
        task["result_status"] = result["status"];
        task["result_message"] = cronMessage(result["message"].toStr());
        task["is_result_unavailable"] = result.Count == 0 && result_json.Length > 0;
        if (!Utils.isEmpty(row["next_run_utc"]))
        {
            var next = row["next_run_utc"].toDate();
            var schedule_zone = FwCron.findZoneOrNull(row["timezone"].toStr());
            task["next_zone"] = schedule_zone?.Id ?? DateUtils.TZ_UTC;
            task["next_time"] = schedule_zone == null ? utcTime(next) : zonedTime(next, schedule_zone, is_include_zone: false);
            task["next_utc"] = schedule_zone != null && schedule_zone.Id != DateUtils.TZ_UTC ? utcTime(next) : "";
        }
        addTimestamp(task, row, "start_date_utc", "start");
        addTimestamp(task, row, "end_date_utc", "end");
        addTimestamp(task, row, "last_started_utc", "last");
        addTimestamp(task, row, "last_finished_utc", "finished");
        return task;
    }

    private FwDict? addReportDisplay(FwDict ps, FwCron.Row task)
    {
        var report = FwReports.reportCode(task.icode);
        if (report.Length == 0)
            return null;

        ps["report_code"] = report;
        try
        {
            var payload = fw.model<FwReports>().parseSchedulePayload(task.icode, task.payload ?? "");
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
                FwReports.checkScheduleTarget(instance, report, payload);
                ps["report_params"] = instance.describeScheduledFilters(parameters);
                ps["can_edit_report_filters"] = task.status != FwModel.STATUS_DELETED;
            }
            catch (Exception ex) when (ex is UserException or AuthException or DbException or ApplicationException)
            {
                ps["is_report_unavailable"] = true;
            }
            ps["report_run_as"] = user;
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
        var zone = FwCron.findZone(fw.userTimezone);
        var local = TimeZoneInfo.ConvertTime(new DateTimeOffset(value), zone);
        task[prefix + "_local"] = value;
        task[prefix + "_zone"] = zone.Id + " (" + local.ToString("zzz", CultureInfo.InvariantCulture) + ")";
        if (zone.Id != DateUtils.TZ_UTC)
            task[prefix + "_utc"] = utcTime(value);
    }

    private string zonedTime(DateTime value, TimeZoneInfo zone, bool is_include_zone = true)
    {
        var local = TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)), zone);
        var format = DateUtils.mapDateFormat(fw.userDateFormat) + " " + DateUtils.mapTimeFormat(fw.userTimeFormat);
        return local.ToString(format, CultureInfo.InvariantCulture)
            + (is_include_zone ? " " + zone.Id + " (" + local.ToString("zzz") + ")" : "");
    }

    private static string utcTime(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private void setScheduleDescription(FwDict task)
    {
        var fields = task["cron"].toStr().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5 || fields[3] != "*"
            || !int.TryParse(fields[0], out var minute) || minute < 0 || minute > 59
            || !int.TryParse(fields[1], out var hour) || hour < 0 || hour > 23)
            return;

        var time = new DateTime(2000, 1, 1, hour, minute, 0);
        if (fields[2] == "*" && fields[4] == "*")
            task["schedule_kind"] = "daily";
        else if (fields[2] == "*" && fields[4] == "1-5")
            task["schedule_kind"] = "weekdays";
        else if (fields[2] == "*" && fields[4].Split(',').All(day => day.Length == 1 && day[0] >= '0' && day[0] <= '6'))
        {
            task["schedule_kind"] = "weekly";
            task["schedule_days"] = new FwList(fields[4].Split(',').Select(day => new FwDict { ["day"] = day }));
        }
        else if (fields[4] == "*" && int.TryParse(fields[2], out var date) && date >= 1 && date <= 31)
        {
            task["schedule_kind"] = "monthly";
            task["schedule_day"] = date;
        }
        else
            return;

        task["schedule_time"] = time.ToString(DateUtils.mapTimeFormat(fw.userTimeFormat), CultureInfo.InvariantCulture);
    }

    private string cronMessage(string code)
    {
        if (code.Length == 0)
            return "";

        var template = code.StartsWith("REPORT_", StringComparison.Ordinal)
            ? "/admin/reports/common/schedule_messages.sel" : "/admin/cron/cron_messages.sel";
        var message = FormUtils.selectTplName(template, code);
        return message.Length > 0 ? fw.parsePageInstance().langMap(message) : code;
    }

    private void finishAction(int id, string message, string flash_type = "success")
    {
        var text = cronMessage(message);
        if (flash_type == "error" && fw.isJsonExpected())
        {
            fw.setGlobalError(text);
            fw.response.StatusCode = 400;
            fw.parserJson((FwDict)afterSave(false, id)!["_json"]!);
            return;
        }

        fw.flash(flash_type, text);
        fw.redirect(base_url + "/" + id, is_exception: false);
    }

    private FwCron.Row taskOrFail(int id, bool is_include_deleted = false)
    {
        var task = model.oneT(id);
        if (task == null || (!is_include_deleted && task.status == FwModel.STATUS_DELETED))
            throw new NotFoundException();

        return task;
    }
}
