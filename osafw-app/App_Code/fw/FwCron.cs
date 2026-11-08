// Scheduled task model and execution state.
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net

namespace osafw;

using Cronos;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

/// <summary>Stops future runs when a task's configuration requires administrator review. Supply a safe plain-text message.</summary>
public class FwCronPauseException(string message) : Exception(message);

/// <summary>Pauses future runs when a handler's external effect could have occurred. Supply a safe plain-text message.</summary>
public class FwCronUncertainException(string message) : Exception(message);

public class FwCron : FwModel<FwCron.Row>
{
    public class Row
    {
        public int id { get; set; }

        public string icode { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public string? idesc { get; set; }

        public string cron { get; set; } = string.Empty;
        public DateTime? next_run_utc { get; set; }
        public DateTime? start_date_utc { get; set; }
        public DateTime? end_date_utc { get; set; }
        public string timezone { get; set; } = "UTC";
        public string? payload { get; set; }

        public string? run_token { get; set; }
        public DateTime? run_requested_utc { get; set; }
        public DateTime? last_started_utc { get; set; }
        public DateTime? last_finished_utc { get; set; }
        public string? last_result { get; set; }

        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public const string ICODE_EXAMPLE = "example_sleep";
    public const string REPORT_PREFIX = "report_";
    public const int STATUS_COMPLETED = 20;
    public const string RESULT_RUNNING = "running";
    public const string RESULT_SUCCEEDED = "succeeded";
    public const string RESULT_FAILED = "failed";
    public const string RESULT_UNCERTAIN = "uncertain";

    private const string DEFAULT_CRON = "0 7 * * *";
    private const string BOUND_FORMAT = "yyyy-MM-dd'T'HH:mm:ss";
    private const int REPORT_SUFFIX_LENGTH = 10;
    private const int REPORT_CODE_MAX_LENGTH = 128;
    private const int RESULT_MESSAGE_MAX_LENGTH = 2000;
    private const int RESULT_MAX_BYTES = 16 * 1024;
    private static readonly TimeSpan RUN_GRACE = TimeSpan.FromMinutes(5);

    public FwCron() : base()
    {
        table_name = "fwcron";
        is_log_fields_changed = false; // Do not copy report delivery settings or filters into activity logs.
    }

    /// <summary>Returns legacy handler codes that Site Admins can use when creating a task.</summary>
    public virtual IReadOnlyList<string> legacyCodes() => [ICODE_EXAMPLE];

    /// <summary>Gets the exact report code from a generated task code, or an empty string for an invalid identity.</summary>
    public static string reportCode(string icode)
    {
        if (!icode.StartsWith(REPORT_PREFIX, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var colon = icode.IndexOf(':');
        if (colon <= REPORT_PREFIX.Length || icode.Length - colon - 1 != REPORT_SUFFIX_LENGTH)
            return string.Empty;

        var code = icode[REPORT_PREFIX.Length..colon];
        return isReportCode(code) && isReportCode(icode[(colon + 1)..]) ? code : string.Empty;
    }

    private static bool isReportCode(string code) =>
        code.Length > 0 && REPORT_PREFIX.Length + code.Length + 1 + REPORT_SUFFIX_LENGTH <= REPORT_CODE_MAX_LENGTH
        && Regex.IsMatch(code, @"\A[A-Za-z0-9_-]+\z");

    /// <summary>Creates a task with a generated report identity. The caller supplies the validated definition.</summary>
    public int addReport(string report_code, FwDict fields)
    {
        if (!isReportCode(report_code))
            throw new ArgumentException("Invalid report code for a scheduled task.", nameof(report_code));

        var item = new FwDict(fields)
        {
            ["icode"] = $"{REPORT_PREFIX}{report_code}:{Utils.nanoid(REPORT_SUFFIX_LENGTH)}"
        };
        return add(item);
    }

    /// <summary>Builds template data for the shared editor. The caller supplies a unique prefix for each instance.</summary>
    internal FwDict cronEditor(FwDict values, string prefix,
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
            ["viewer_zone"] = fw.userTimezone
        };
    }

    /// <summary>Validates an editable schedule and resolves posted viewer-local bounds into UTC.</summary>
    internal void prepareSchedule(FwDict fields, Row? observed = null)
    {
        var (cron, zone, start, end) = readSchedule(fields, observed);
        var requestedStatus = fields["status"].toStr("0");
        if (requestedStatus != "0" && requestedStatus != "10" &&
            !(requestedStatus == "20" && observed?.status == FwCron.STATUS_COMPLETED))
            throw new UserException("CRON_STATUS");

        var status = requestedStatus.toInt();
        var is_changed = observed == null || observed.cron != cron || observed.timezone != zone.Id
            || observed.start_date_utc != start || observed.end_date_utc != end;
        var is_recalculate = is_changed || (status == FwModel.STATUS_ACTIVE && observed?.status != status);
        var next = is_recalculate ? FwCron.calculateNextRun(cron, DateTime.UtcNow, end, zone, start) : observed?.next_run_utc;
        if (is_recalculate && status == FwModel.STATUS_ACTIVE && !next.HasValue)
            throw new UserException("CRON_NO_FUTURE");

        fields["cron"] = cron;
        fields["timezone"] = zone.Id;
        fields["start_date_utc"] = start;
        fields["end_date_utc"] = end;
        fields["next_run_utc"] = next;
        fields["status"] = status;
    }

    internal (string cron, TimeZoneInfo zone, DateTime? start, DateTime? end) readSchedule(
        FwDict values, Row? observed = null)
    {
        var cron = values["cron"].toStr().Trim();
        if (cron.Length > 255 || !CronExpression.TryParse(cron, out _))
            throw new UserException("CRON_INVALID");

        var zone = findZone(values["timezone"].toStr().Trim());
        var start = readBound(values["start_date_utc"], observed?.start_date_utc);
        var end = readBound(values["end_date_utc"], observed?.end_date_utc);
        if (start.HasValue && end.HasValue && start >= end)
            throw new UserException("CRON_END");

        return (cron, zone, start, end);
    }

    internal static TimeZoneInfo findZone(string id) =>
        findZoneOrNull(id) ?? throw new UserException("CRON_ZONE");

    internal static TimeZoneInfo? findZoneOrNull(string id) =>
        id.Length > 0 && id.Length <= 64 && !id.Equals("Auto", StringComparison.OrdinalIgnoreCase)
            && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : null;

    private DateTime? readBound(object? value, DateTime? observed)
    {
        var raw = value.toStr().Trim();
        if (raw.Length == 0)
            return null;

        var zone = findZone(fw.userTimezone);
        if (!DateTime.TryParseExact(raw, [BOUND_FORMAT, "yyyy-MM-dd'T'HH:mm"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var local) || zone.IsInvalidTime(local))
            throw new UserException("CRON_BOUND");

        // Keep the original instant when the unchanged clock time falls in a daylight-saving fold.
        if (observed.HasValue && boundInput(observed) == local.ToString(BOUND_FORMAT, CultureInfo.InvariantCulture))
            return observed;

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    internal string boundInput(DateTime? value) =>
        value.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(value.Value, findZone(fw.userTimezone))
            .ToString(BOUND_FORMAT, CultureInfo.InvariantCulture) : "";

    /// <summary>Returns a recognized latest-result envelope, or an empty dictionary if it is unavailable.</summary>
    public static FwDict parseResult(string? json)
    {
        if (string.IsNullOrEmpty(json) || Encoding.UTF8.GetByteCount(json) > RESULT_MAX_BYTES)
            return [];

        var result = Utils.jsonDecodeDict(json);
        if (result is null || result["message"] is not string
            || result["status"] is not (RESULT_RUNNING or RESULT_SUCCEEDED or RESULT_FAILED or RESULT_UNCERTAIN))
            return [];

        try
        {
            using var document = JsonDocument.Parse(json);
            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string resultJson(string status, string message, object? data = null)
    {
        message = boundMessage(message);
        var result = new FwDict
        {
            ["status"] = status,
            ["message"] = message
        };
        if (data != null)
        {
            result["data"] = data;
            try
            {
                var json = Utils.jsonEncode(result);
                if (Encoding.UTF8.GetByteCount(json) <= RESULT_MAX_BYTES)
                    return json;
            }
            catch (Exception)
            {
                // Optional data cannot alter the known outcome of a completed handler.
            }

            result.Remove("data");
        }

        return Utils.jsonEncode(result);
    }

    private static string boundMessage(string message) =>
        message.Length <= RESULT_MESSAGE_MAX_LENGTH ? message : message[..RESULT_MESSAGE_MAX_LENGTH];

    private static bool isRunnableStatus(int status) => status is STATUS_ACTIVE or STATUS_INACTIVE;

    /// <summary>Whether the hosted scheduler is registered in this build.</summary>
    public static bool isRunnerEnabled()
    {
#if isFwCronService
        return true;
#else
        return false;
#endif
    }

    /// <summary>Queues one manual request for an idle active or paused task within its end bound.</summary>
    public bool requestRun(int id)
    {
        var job = oneT(id);
        var now = DateTime.UtcNow;
        if (job == null || !isRunnableStatus(job.status) || job.end_date_utc <= now)
            return false;

        var requested = writeTask(new FwDict { ["run_requested_utc"] = now }, new()
        {
            ["id"] = id,
            ["status"] = job.status,
            ["end_date_utc"] = job.end_date_utc,
            ["run_token"] = null,
            ["run_requested_utc"] = null
        });

        if (requested)
            fw.logActivity(FwLogTypes.ICODE_CRON_EVENT, table_name, id, "Manual run requested.");
        return requested;
    }

    /// <summary>Pauses future occurrences and cancels any pending manual request. A held run can still finish.</summary>
    public bool pause(int id)
    {
        var job = oneT(id);
        if (job == null || !isRunnableStatus(job.status))
            return false;

        var where = new FwDict
        {
            ["id"] = id,
            ["status"] = job.status
        };
        if (job.status == STATUS_INACTIVE)
            where["run_requested_utc"] = db.opISNOTNULL();

        var changed = writeDefinition(new()
        {
            ["status"] = STATUS_INACTIVE,
            ["run_requested_utc"] = null
        }, where);

        if (changed)
            fw.logActivity(FwLogTypes.ICODE_CRON_EVENT, table_name, id, "Task paused.");
        return changed;
    }

    /// <summary>Deletes an idle task, retaining application tasks so their fixed job codes can be restored.</summary>
    public bool deleteTask(int id)
    {
        var task = oneT(id);
        if (task == null)
            return false;

        var where = new FwDict
        {
            ["id"] = id,
            ["icode"] = task.icode,
            ["status"] = task.status,
            ["run_token"] = null
        };
        if (isReport(task))
        {
            if (db.del(table_name, where) != 1)
                return false;

            removeCache(id);
        }
        else if (task.status == STATUS_DELETED || !writeDefinition(new()
        {
            ["status"] = STATUS_DELETED,
            ["run_requested_utc"] = null
        }, where))
        {
            return false;
        }

        fw.logActivity(FwLogTypes.ICODE_DELETED, table_name, id);
        return true;
    }

    /// <summary>Restores an idle, deleted application task as Paused without requesting execution.</summary>
    public bool restoreTask(int id)
    {
        var task = oneT(id);
        if (task?.status != STATUS_DELETED || isReport(task))
            return false;

        var changed = writeDefinition(new()
        {
            ["status"] = STATUS_INACTIVE,
            ["run_requested_utc"] = null
        }, new()
        {
            ["id"] = id,
            ["icode"] = task.icode,
            ["status"] = STATUS_DELETED,
            ["run_token"] = null
        });

        if (changed)
            fw.logActivity(FwLogTypes.ICODE_UPDATED, table_name, id, "Task restored as paused.");
        return changed;
    }

    /// <summary>Resumes an unchanged, idle paused or completed task at its first future occurrence.</summary>
    public bool resume(Row observed)
    {
        if (observed.status is not (STATUS_INACTIVE or STATUS_COMPLETED))
            return false;

        var next = nextOccurrence(observed, DateTime.UtcNow);
        return next.HasValue && saveDefinition(observed.id, observed, new()
        {
            ["status"] = STATUS_ACTIVE,
            ["next_run_utc"] = next
        }, "Task resumed.");
    }

    /// <summary>
    /// Updates validated editable fields if the task is idle and its definition and schedule still match the observed row.
    /// The caller supplies a recalculated next run for recurrence changes.
    /// </summary>
    public bool saveDefinition(int id, Row observed, FwDict fields) =>
        saveDefinition(id, observed, fields, "Task definition updated.");

    private bool saveDefinition(int id, Row observed, FwDict fields, string message)
    {
        if (id != observed.id || observed.status == STATUS_DELETED)
            return false;

        var values = new FwDict(fields);
        foreach (var field in values.Keys)
        {
            if (field is not ("iname" or "idesc" or "cron" or "timezone" or "start_date_utc" or "end_date_utc" or "next_run_utc" or "status" or "payload"))
                throw new ArgumentException("Field is not part of the editable task definition.", nameof(fields));
        }

        values["run_requested_utc"] = null;
        var where = scheduleWhere(observed);
        where["iname"] = observed.iname;
        where["idesc"] = observed.idesc;
        where["payload"] = observed.payload;
        where["upd_time"] = observed.upd_time;
        var changed = writeDefinition(values, where);
        if (changed)
            fw.logActivity(FwLogTypes.ICODE_UPDATED, table_name, id, message);
        return changed;
    }

    /// <summary>Releases the observed claim as uncertain and paused after an administrator has verified the worker stopped.</summary>
    public bool recover(int id, string token)
    {
        if (string.IsNullOrEmpty(token) || !string.Equals(oneField(id, "run_token").toStr(), token, StringComparison.Ordinal))
            return false;

        const string message = "Execution interrupted. Verify the external outcome before requesting another run.";
        var recovered = writeDefinition(new()
        {
            ["status"] = STATUS_INACTIVE,
            ["run_token"] = null,
            ["run_requested_utc"] = null,
            ["last_finished_utc"] = DateTime.UtcNow,
            ["last_result"] = resultJson(RESULT_UNCERTAIN, "CRON_INTERRUPTED")
        }, new()
        {
            ["id"] = id,
            ["run_token"] = token
        });

        if (recovered)
            fw.logActivity(FwLogTypes.ICODE_CRON_EVENT, table_name, id, message);
        return recovered;
    }

    /// <summary>
    /// Lists candidate rows for the worker, including pending manual requests and active schedules that need initialization
    /// or completion. Eligibility is checked again before claiming a row.
    /// </summary>
    public List<Row> listDueJobs()
    {
        return db.arrayp<Row>($@"
            SELECT *
            FROM {qTable()}
            WHERE
                run_token IS NULL
                AND (
                    (
                        status = @active
                        AND (
                            next_run_utc IS NULL
                            OR next_run_utc <= @now_utc
                            OR end_date_utc <= @now_utc
                        )
                    )
                    OR (
                        status IN (@active, @paused)
                        AND run_requested_utc IS NOT NULL
                    )
                )
            ORDER BY next_run_utc, id",
            new()
            {
                ["active"] = STATUS_ACTIVE,
                ["paused"] = STATUS_INACTIVE,
                ["now_utc"] = DateTime.UtcNow
            });
    }

    /// <summary>Claims an eligible task, executes its current definition, then releases only that claim.</summary>
    public void runJob(Row job)
    {
        var snapshot = claimTask(job.id);
        if (snapshot == null)
            return;

        var status = RESULT_UNCERTAIN;
        var message = "CRON_OUTCOME_UNAVAILABLE";
        object? data = null;
        var is_pause = true;

        try
        {
            // Uncomment to record each run during development.
            // fw.logActivity(FwLogTypes.ICODE_CRON_EVENT, table_name, job.id, "Task run started.");
            FwDict? details;
            if (isReport(snapshot))
            {
                if (reportCode(snapshot.icode).Length == 0)
                    throw new FwCronPauseException("CRON_BAD_REPORT_IDENTITY");

                details = fw.model<FwScheduledReports>().run(snapshot);
            }
            else
            {
                details = runJobAction(snapshot);
            }

            status = RESULT_SUCCEEDED;
            message = details?["message"] as string ?? "CRON_COMPLETED";
            data = details?["data"];
            is_pause = false;
        }
        catch (FwCronUncertainException ex)
        {
            message = ex.Message;
        }
        catch (FwCronPauseException ex)
        {
            status = RESULT_FAILED;
            message = ex.Message;
        }
        catch (Exception)
        {
            status = RESULT_FAILED;
            message = isReport(snapshot) ? "CRON_REPORT_GENERATION_FAILED" : "CRON_HANDLER_FAILED";
            is_pause = false;
        }
        finally
        {
            finish(snapshot, status, message, data, is_pause);
        }
    }

    private Row? claimTask(int id)
    {
        // A due-list entry may be stale by the time the worker reaches it.
        var job = db.row<Row>(table_name, DB.h("id", id));
        if (job == null || job.run_token != null || !isRunnableStatus(job.status)
            || (job.status == STATUS_INACTIVE && !job.run_requested_utc.HasValue))
            return null;

        var now = DateTime.UtcNow;
        var fields = new FwDict();
        var events = new List<string>();
        var is_manual = job.run_requested_utc.HasValue;
        var is_due = false;
        if (is_manual && job.run_requested_utc < now - RUN_GRACE)
        {
            events.Add($"Manual request from {eventTime(job.run_requested_utc!.Value)} expired.");
            fields["run_requested_utc"] = null;
            is_manual = false;
        }

        if (job.end_date_utc <= now)
        {
            if (job.status == STATUS_ACTIVE)
            {
                fields["status"] = STATUS_COMPLETED;
                fields["next_run_utc"] = null;
            }
            if (is_manual)
            {
                fields["run_requested_utc"] = null;
                events.Add("Manual request canceled because the schedule ended.");
                is_manual = false;
            }
        }
        else
        {
            var is_before_start = job.next_run_utc < job.start_date_utc;
            is_due = job.status == STATUS_ACTIVE && job.next_run_utc.HasValue && job.next_run_utc <= now && !is_before_start;
            if (job.status == STATUS_ACTIVE && (!job.next_run_utc.HasValue || is_before_start || is_due))
            {
                if (!trySchedule(job, out var zone))
                {
                    fields["status"] = STATUS_INACTIVE;
                    fields["run_requested_utc"] = null;
                    events.Add("Task paused because its cron expression or timezone is invalid.");
                    is_manual = false;
                    is_due = false;
                }
                else
                {
                    var next = calculateNextRun(job.cron, now, job.end_date_utc, zone, job.start_date_utc);
                    fields["next_run_utc"] = next;
                    if (!job.next_run_utc.HasValue && !job.start_date_utc.HasValue && !isReport(job))
                        fields["start_date_utc"] = now;

                    if (is_due && isReport(job) && job.next_run_utc < now - RUN_GRACE)
                    {
                        events.Add($"Skipped overdue report occurrences beginning at {eventTime(job.next_run_utc!.Value)}.");
                        is_due = false;
                    }

                    if (!next.HasValue && !is_manual && !is_due)
                        fields["status"] = STATUS_COMPLETED;
                }
            }
        }

        string? token = null;
        if (is_manual || is_due)
        {
            token = Utils.nanoid();
            fields["run_token"] = token;
            fields["last_started_utc"] = now;
            fields["last_finished_utc"] = null;
            fields["last_result"] = resultJson(RESULT_RUNNING, "CRON_RUNNING");
            fields["run_requested_utc"] = null;
        }

        var where = scheduleWhere(job);
        where["run_requested_utc"] = job.run_requested_utc;
        if (!writeTask(fields, where))
            return null;

        foreach (var message in events)
            fw.logActivity(FwLogTypes.ICODE_CRON_EVENT, table_name, id, message);

        if (token == null)
            return null;

        var claimed = oneT(id);
        return claimed?.run_token == token ? claimed : null;
    }

    private void finish(Row snapshot, string status, string message, object? data, bool is_pause)
    {
        var now = DateTime.UtcNow;
        var fields = new FwDict
        {
            ["run_token"] = null,
            ["last_finished_utc"] = now,
            ["last_result"] = resultJson(status, message, data)
        };
        var where = new FwDict
        {
            ["id"] = snapshot.id,
            ["run_token"] = snapshot.run_token
        };

        var next = snapshot.next_run_utc;
        var is_skipped = isReport(snapshot) && next <= now && next > snapshot.last_started_utc;
        if (is_skipped)
        {
            next = nextOccurrence(snapshot, now);
            fields["next_run_utc"] = next;
        }

        var is_finished = false;
        if (is_pause)
        {
            fields["status"] = STATUS_INACTIVE;
        }
        else if (!next.HasValue || snapshot.end_date_utc <= now)
        {
            var completed = new FwDict(fields)
            {
                ["status"] = STATUS_COMPLETED,
                ["next_run_utc"] = null
            };
            where["status"] = STATUS_ACTIVE;
            is_finished = writeTask(completed, where);

            // An administrator can pause a held run. Preserve that status when releasing the claim.
            where.Remove("status");
        }

        if (!is_finished && !writeTask(fields, where))
            return;

        if (is_skipped)
            fw.logActivity(FwLogTypes.ICODE_CRON_EVENT, table_name, snapshot.id, "Skipped report occurrences during execution.");
        if (status != RESULT_SUCCEEDED)
            fw.logActivity(FwLogTypes.ICODE_CRON_EVENT, table_name, snapshot.id, boundMessage(message));
        // Uncomment to record successful runs during development.
        // if (status == RESULT_SUCCEEDED)
        //     fw.logActivity(FwLogTypes.ICODE_CRON_EVENT, table_name, snapshot.id, "Task run succeeded.");
    }

    // Match the scheduling state used to calculate an update and require the task to be idle.
    private static FwDict scheduleWhere(Row job) => new()
    {
        ["id"] = job.id,
        ["icode"] = job.icode,
        ["status"] = job.status,
        ["cron"] = job.cron,
        ["timezone"] = job.timezone,
        ["start_date_utc"] = job.start_date_utc,
        ["end_date_utc"] = job.end_date_utc,
        ["next_run_utc"] = job.next_run_utc,
        ["run_token"] = null
    };

    private bool writeTask(FwDict fields, FwDict where)
    {
        if (fields.Count == 0 || db.update(table_name, fields, where) != 1)
            return false;

        removeCache(where["id"].toInt());
        return true;
    }

    private bool writeDefinition(FwDict fields, FwDict where)
    {
        fields["upd_time"] = DB.NOW;
        if (fw.isLogged)
            fields["upd_users_id"] = fw.userId;

        return writeTask(fields, where);
    }

    private static string eventTime(DateTime date) => date.ToString("u", CultureInfo.InvariantCulture);

    internal static bool isReport(Row job) => job.icode.StartsWith(REPORT_PREFIX, StringComparison.OrdinalIgnoreCase);

    private static bool trySchedule(Row job, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(job.timezone) || !CronExpression.TryParse(job.cron, out _))
            return false;

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(job.timezone);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static DateTime? nextOccurrence(Row job, DateTime now) =>
        trySchedule(job, out var zone) ? calculateNextRun(job.cron, now, job.end_date_utc, zone, job.start_date_utc) : null;

    internal static DateTime? calculateNextRun(string cron, DateTime from_date, DateTime? end_date) =>
        calculateNextRun(cron, from_date, end_date, TimeZoneInfo.Utc);

    /// <summary>Calculates the first UTC occurrence after the reference instant within the inclusive start and exclusive end.</summary>
    public static DateTime? calculateNextRun(string cron, DateTime from_date, DateTime? end_date, TimeZoneInfo zone,
        DateTime? start_date = null)
    {
        if (!CronExpression.TryParse(cron, out var expression) || end_date <= from_date)
            return null;

        var is_inclusive = start_date > from_date;
        var anchor = is_inclusive ? start_date!.Value : from_date;
        var next = expression.GetNextOccurrence(DateTime.SpecifyKind(anchor, DateTimeKind.Utc), zone, is_inclusive);
        return next >= end_date ? null : next;
    }

    /// <summary>Executes an application job. Return optional message and JSON-compatible data, or null for a generic result.</summary>
    protected virtual FwDict? runJobAction(Row job)
    {
        switch (job.icode)
        {
            case ICODE_EXAMPLE:
                Thread.Sleep(2000);
                return null;
            default:
                throw new FwCronPauseException("CRON_UNKNOWN_HANDLER");
        }
    }
}
