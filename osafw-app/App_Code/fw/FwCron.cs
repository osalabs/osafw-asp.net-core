// Scheduled task model and execution state.
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net

namespace osafw;

using Cronos;
using Microsoft.Data.SqlClient;
using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
#if isSQLite
using Microsoft.Data.Sqlite;
#endif

public class TFwCron
{
    public int id { get; set; }

    public string icode { get; set; } = string.Empty;
    public string iname { get; set; } = string.Empty;
    public string idesc { get; set; } = string.Empty;

    public string cron { get; set; } = string.Empty;
    public DateTime? next_run { get; set; }
    public DateTime? start_date { get; set; }
    public DateTime? end_date { get; set; }
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
    public DateTime upd_time { get; set; }
    public int upd_users_id { get; set; }
}

/// <summary>Stops future runs when a task's configuration requires administrator review. Supply a safe plain-text message.</summary>
public class FwCronPauseException(string message) : Exception(message);

/// <summary>Pauses future runs when a handler's external effect could have occurred. Supply a safe plain-text message.</summary>
public class FwCronUncertainException(string message) : Exception(message);

public class FwCron : FwModel
{
    public const string ICODE_EXAMPLE = "example_sleep";
    public const string REPORT_PREFIX = "report_";
    public const int STATUS_COMPLETED = 20;
    public const string RESULT_RUNNING = "running";
    public const string RESULT_SUCCEEDED = "succeeded";
    public const string RESULT_FAILED = "failed";
    public const string RESULT_UNCERTAIN = "uncertain";

    private const int REPORT_SUFFIX_LENGTH = 10;
    private const int REPORT_CODE_MAX_LENGTH = 128;
    private const int REPORT_CODE_INSERT_ATTEMPTS = 3;
    private const int RESULT_MESSAGE_MAX_LENGTH = 2000;
    private const int RESULT_MAX_BYTES = 16 * 1024;
    private const string STORAGE_FOLD_MESSAGE = "A schedule date falls in a repeated local hour that the database cannot represent precisely. Verify the database timezone and storage with an application developer.";
    private static readonly TimeSpan RUN_GRACE = TimeSpan.FromMinutes(5);

    public FwCron() : base()
    {
        table_name = "fwcron";
        is_log_changes = false;
    }

    /// <summary>Returns legacy handler codes that Site Admins can use when creating a task.</summary>
    public virtual IReadOnlyList<string> legacyCodes() => [ICODE_EXAMPLE];

    /// <summary>Reads the current row directly, including its latest execution state, or null for a missing ID.</summary>
    public TFwCron? getTask(int id) => db.row<TFwCron>(table_name, DB.h("id", id));

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

    /// <summary>Rejects a schedule instant whose database-local clock time cannot retain its offset.</summary>
    public void validateScheduleDates(DateTime? start_date, DateTime? end_date, DateTime? next_run)
    {
        if (!start_date.HasValue && !end_date.HasValue && !next_run.HasValue)
            return;

        if (!db.isTimezoneDetectionOk())
            db.connect();

        if (hasAmbiguousScheduleDate(storageZone(), start_date, end_date, next_run))
            throw new UserException(STORAGE_FOLD_MESSAGE);
    }

    public override int add(FwDict item)
    {
        validateWrittenDates(item);
        return base.add(item);
    }

    public override bool update(int id, FwDict item)
    {
        validateWrittenDates(item);
        return base.update(id, item);
    }

    private void validateWrittenDates(FwDict item) =>
        validateScheduleDates(scheduleDate(item["start_date"]), scheduleDate(item["end_date"]), scheduleDate(item["next_run"]));

    private DateTime? scheduleDate(object? value)
    {
        if (ReferenceEquals(value, DB.NOW))
            return db.Now();
        return value == null ? null : db.qd(value);
    }

    private TimeZoneInfo storageZone() => TimeZoneInfo.FindSystemTimeZoneById(db.getTimezoneId());

    private static bool hasAmbiguousScheduleDate(TimeZoneInfo zone, DateTime? start, DateTime? end, DateTime? next) =>
        isAmbiguousStorageTime(zone, start) || isAmbiguousStorageTime(zone, end) || isAmbiguousStorageTime(zone, next);

    private static bool isAmbiguousStorageTime(TimeZoneInfo zone, DateTime? date)
    {
        if (!date.HasValue || zone.Equals(TimeZoneInfo.Utc))
            return false;

        var local = date.Value;
        // DB helpers already interpret Unspecified values as database-local wall time.
        if (local.Kind != DateTimeKind.Unspecified)
            local = TimeZoneInfo.ConvertTimeFromUtc(local.ToUniversalTime(), zone);

        return zone.IsAmbiguousTime(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
    }

    /// <summary>Creates a task with a generated report identity. The caller supplies the validated definition.</summary>
    public int addReport(string report_code, FwDict fields)
    {
        if (!isReportCode(report_code))
            throw new ArgumentException("Invalid report code for a scheduled task.", nameof(report_code));

        var item = new FwDict(fields);
        for (var attempt = 1; ; attempt++)
        {
            item["icode"] = $"{REPORT_PREFIX}{report_code}:{Utils.nanoid(REPORT_SUFFIX_LENGTH)}";
            int id;
            try
            {
                id = add(item);
            }
            catch (Exception ex) when (attempt < REPORT_CODE_INSERT_ATTEMPTS && isReportCodeConflict(ex))
            {
                continue;
            }

            logEvent(id, "Report task created.");
            return id;
        }
    }

    private static bool isReportCodeConflict(Exception ex)
    {
        if (ex is SqlException sql && sql.Number == 2601)
            return sql.Message.Contains("'UX_fwcron_icode'", StringComparison.OrdinalIgnoreCase);
#if isSQLite
        if (ex is SqliteException sqlite && sqlite.SqliteExtendedErrorCode == 2067)
            return sqlite.Message.Contains("UNIQUE constraint failed: fwcron.icode'", StringComparison.OrdinalIgnoreCase);
#endif
        return false;
    }

    /// <summary>Adds a literal report identity prefix filter for a SQL Server or SQLite task list.</summary>
    public string reportFilterSql(string report_code, FwDict parameters)
    {
        if (!isReportCode(report_code))
            return "1 = 0";

        var prefix = REPORT_PREFIX + report_code + ":";
        if (db.dbtype == DB.DBTYPE_SQLITE)
        {
            // SQLite LIKE ignores ASCII case even for a BINARY column. Match the report code's equality rules.
            parameters["report_prefix"] = prefix;
            return "substr(icode, 1, length(@report_prefix)) = @report_prefix COLLATE BINARY";
        }

        parameters["report_prefix"] = prefix.Replace("~", "~~").Replace("_", "~_").Replace("%", "~%").Replace("[", "~[") + "%";
        return "icode LIKE @report_prefix ESCAPE '~'";
    }

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
            result["message"] = boundMessage("Result details unavailable. " + message);
        }

        return Utils.jsonEncode(result);
    }

    private static string boundMessage(string message) =>
        message.Length <= RESULT_MESSAGE_MAX_LENGTH ? message : message[..RESULT_MESSAGE_MAX_LENGTH];

    private static bool isRunnableStatus(int status) => status is STATUS_ACTIVE or STATUS_INACTIVE;

    /// <summary>Queues one manual request for an idle active or paused task within its end bound.</summary>
    public bool requestRun(int id)
    {
        DateTime now = default;
        var requested = changeTask(id, job =>
        {
            now = DateTime.UtcNow;
            return job.run_token is null && !job.run_requested_utc.HasValue
                && isRunnableStatus(job.status) && !(job.end_date <= now)
                && writeTask(id, DB.h("run_requested_utc", now));
        });

        if (requested)
            logEvent(id, $"Manual run requested at {eventTime(now)}.");
        return requested;
    }

    /// <summary>Pauses future occurrences and cancels any pending manual request. A held run can still finish.</summary>
    public bool pause(int id) => setInactive(id, STATUS_INACTIVE);

    /// <summary>Soft-deletes an idle task and cancels any pending manual request.</summary>
    public bool deleteTask(int id) => setInactive(id, STATUS_DELETED);

    private bool setInactive(int id, int status)
    {
        var is_pause = status == STATUS_INACTIVE;
        var message = is_pause ? "Task paused." : "Task deleted.";
        var changed = changeTask(id, job =>
        {
            var is_already_paused_with_request = job.status == STATUS_INACTIVE && job.run_requested_utc.HasValue;
            if (is_pause && job.status != STATUS_ACTIVE && !is_already_paused_with_request)
                return false;
            if (!is_pause && (job.status == STATUS_DELETED || job.run_token != null))
                return false;

            message += canceledRequest(job);
            return writeDefinition(id, new()
            {
                ["status"] = status,
                ["run_requested_utc"] = null
            });
        });
        if (changed)
            logEvent(id, message);
        return changed;
    }

    /// <summary>Resumes an unchanged, idle paused or completed task at its first future occurrence.</summary>
    public bool resume(TFwCron observed)
    {
        if (observed.status is not (STATUS_INACTIVE or STATUS_COMPLETED))
            return false;

        validateScheduleDates(observed.start_date, observed.end_date, null);
        var next = nextOccurrence(observed, DateTime.UtcNow);
        return next.HasValue && saveDefinition(observed.id, observed, new()
        {
            ["status"] = STATUS_ACTIVE,
            ["next_run"] = next
        }, "Task resumed.");
    }

    /// <summary>
    /// Updates validated editable fields if the task is idle and its definition and schedule still match the observed row.
    /// The caller supplies a recalculated next run for recurrence changes.
    /// </summary>
    public bool saveDefinition(int id, TFwCron observed, FwDict fields) =>
        saveDefinition(id, observed, fields, "Task definition updated.");

    private bool saveDefinition(int id, TFwCron observed, FwDict fields, string message)
    {
        var values = new FwDict(fields);
        foreach (var field in values.Keys)
        {
            if (field is not ("iname" or "idesc" or "cron" or "timezone" or "start_date" or "end_date" or "next_run" or "status" or "payload"))
                throw new ArgumentException("Field is not part of the editable task definition.", nameof(fields));
        }

        values["run_requested_utc"] = null;
        var changed = changeTask(id, job =>
        {
            if (job.status == STATUS_DELETED || job.run_token != null || !isSameDefinition(job, observed))
                return false;

            validateScheduleDates(
                values.ContainsKey("start_date") ? scheduleDate(values["start_date"]) : job.start_date,
                values.ContainsKey("end_date") ? scheduleDate(values["end_date"]) : job.end_date,
                values.ContainsKey("next_run") ? scheduleDate(values["next_run"]) : job.next_run);
            message += canceledRequest(job);
            return writeDefinition(id, values);
        });
        if (changed)
            logEvent(id, message);
        return changed;
    }

    private static bool isSameDefinition(TFwCron current, TFwCron observed) =>
        current.id == observed.id && current.icode == observed.icode && current.iname == observed.iname
        && current.idesc == observed.idesc && current.payload == observed.payload
        && current.cron == observed.cron && current.timezone == observed.timezone
        && current.start_date == observed.start_date && current.end_date == observed.end_date
        && current.next_run == observed.next_run && current.status == observed.status;

    private static string canceledRequest(TFwCron job) =>
        job.run_requested_utc.HasValue ? " Pending manual request canceled." : string.Empty;

    /// <summary>Releases the observed claim as uncertain and paused after an administrator has verified the worker stopped.</summary>
    public bool recover(int id, string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        var now = DateTime.UtcNow;
        const string message = "Execution interrupted. Verify the external outcome before requesting another run.";
        var recovered = changeTask(id, job => job.run_token == token && writeClaim(job, new()
        {
            ["status"] = STATUS_INACTIVE,
            ["run_token"] = null,
            ["run_requested_utc"] = null,
            ["last_finished_utc"] = now,
            ["last_result"] = resultJson(RESULT_UNCERTAIN, message)
        }, is_definition_change: true));

        if (recovered)
            logEvent(id, $"Claim recovered at {eventTime(now)}. {message}");
        return recovered;
    }

    /// <summary>
    /// Lists candidate rows for the worker, including pending manual requests and active schedules that need initialization
    /// or completion. Eligibility and the definition are checked again under the claim lock.
    /// </summary>
    public List<TFwCron> listDueJobs()
    {
        return db.arrayp<TFwCron>($@"
            SELECT *
            FROM {qTable()}
            WHERE
                run_token IS NULL
                AND (
                    (
                        status = @active
                        AND (
                            next_run IS NULL
                            OR next_run <= @now
                            OR end_date <= @now
                        )
                    )
                    OR (
                        status IN (@active, @paused)
                        AND run_requested_utc IS NOT NULL
                    )
                )
            ORDER BY next_run, id",
            new()
            {
                ["active"] = STATUS_ACTIVE,
                ["paused"] = STATUS_INACTIVE,
                ["now"] = DateTime.UtcNow
            });
    }

    /// <summary>
    /// Re-reads and claims an eligible task by ID, executes outside the row transaction, then releases only that claim.
    /// The worker uses a fresh offline FW context for report execution.
    /// </summary>
    public void runJob(TFwCron job)
    {
        var claim = claimTask(job.id);
        if (claim == null)
            return;

        var (snapshot, trigger) = claim.Value;
        var status = RESULT_UNCERTAIN;
        var message = "Execution outcome unavailable.";
        object? data = null;
        var is_pause = true;

        try
        {
            logEvent(job.id, $"Run started at {eventTime(snapshot.last_started_utc!.Value)}. {trigger}");
            FwDict? details;
            if (isReport(snapshot))
            {
                if (reportCode(snapshot.icode).Length == 0)
                    throw new FwCronPauseException("The task's report identity is invalid.");

                details = fw.model<FwScheduledReports>().run(snapshot);
            }
            else
            {
                details = runJobAction(snapshot);
            }

            status = RESULT_SUCCEEDED;
            message = details?["message"] as string ?? "Completed";
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
            message = isReport(snapshot) ? "Report generation failed before sending." : "Task handler failed.";
            is_pause = false;
        }
        finally
        {
            finish(snapshot, trigger, status, message, data, is_pause);
        }
    }

    private (TFwCron Job, string Trigger)? claimTask(int id)
    {
        TFwCron? snapshot = null;
        string trigger = "";
        var events = new List<string>();
        var changed = changeTask(id, job =>
        {
            if (job.run_token != null || !isRunnableStatus(job.status)
                || (job.status == STATUS_INACTIVE && !job.run_requested_utc.HasValue))
                return false;

            var storage_zone = storageZone();
            if (hasAmbiguousScheduleDate(storage_zone, job.start_date, job.end_date, job.next_run))
            {
                events.Add("Task paused. " + STORAGE_FOLD_MESSAGE + canceledRequest(job));
                return writeTask(id, new()
                {
                    ["status"] = STATUS_INACTIVE,
                    ["run_requested_utc"] = null
                });
            }

            var now = DateTime.UtcNow;
            var is_manual = job.run_requested_utc.HasValue;
            var fields = new FwDict();
            if (is_manual && job.run_requested_utc < now - RUN_GRACE)
            {
                events.Add($"Manual request from {eventTime(job.run_requested_utc!.Value)} expired.");
                fields["run_requested_utc"] = null;
                is_manual = false;
            }

            if (job.end_date <= now)
            {
                if (job.status == STATUS_ACTIVE)
                {
                    fields["status"] = STATUS_COMPLETED;
                    fields["next_run"] = null;
                }
                if (is_manual)
                {
                    fields["run_requested_utc"] = null;
                    events.Add("Manual request canceled because the schedule ended.");
                }
                return writeTask(id, fields);
            }

            var is_before_start = job.next_run < job.start_date;
            var is_due = job.status == STATUS_ACTIVE && job.next_run.HasValue && job.next_run <= now
                && !is_before_start;
            if (job.status == STATUS_ACTIVE && (!job.next_run.HasValue || is_before_start || is_due))
            {
                if (!trySchedule(job, out var zone))
                {
                    fields["status"] = STATUS_INACTIVE;
                    fields["run_requested_utc"] = null;
                    events.Add("Task paused because its cron expression or timezone is invalid." + canceledRequest(job));
                    return writeTask(id, fields);
                }

                var next = calculateNextRun(job.cron, now, job.end_date, zone, job.start_date);
                var is_ambiguous_next = isAmbiguousStorageTime(storage_zone, next);
                fields["next_run"] = is_ambiguous_next ? null : next;
                if (is_ambiguous_next)
                {
                    fields["status"] = STATUS_INACTIVE;
                    events.Add("Task paused before scheduling its next occurrence. " + STORAGE_FOLD_MESSAGE);
                }

                if (!job.next_run.HasValue && !job.start_date.HasValue && !isReport(job)
                    && !isAmbiguousStorageTime(storage_zone, now))
                    fields["start_date"] = now;

                if (is_due && isReport(job) && job.next_run < now - RUN_GRACE)
                {
                    events.Add($"Skipped overdue report occurrences beginning at {eventTime(job.next_run!.Value)}.");
                    is_due = false;
                }

                if (!next.HasValue && !is_manual && !is_due)
                    fields["status"] = STATUS_COMPLETED;
            }

            if (!is_manual && !is_due)
                return writeTask(id, fields);

            trigger = runTrigger(job, is_manual, is_due);
            job.run_token = Utils.nanoid();
            job.last_started_utc = now;
            job.last_finished_utc = null;
            job.last_result = resultJson(RESULT_RUNNING, "Running");
            job.run_requested_utc = null;
            fields["run_token"] = job.run_token;
            fields["last_started_utc"] = job.last_started_utc;
            fields["last_finished_utc"] = null;
            fields["last_result"] = job.last_result;
            fields["run_requested_utc"] = null;
            if (!writeTask(id, fields))
                return false;

            if (fields.ContainsKey("next_run"))
                job.next_run = (DateTime?)fields["next_run"];
            if (fields.ContainsKey("start_date"))
                job.start_date = (DateTime?)fields["start_date"];
            snapshot = job;
            return true;
        });

        if (!changed)
            return null;

        foreach (var message in events)
            logEvent(id, message);

        return snapshot == null ? null : (snapshot, trigger);
    }

    private static string runTrigger(TFwCron job, bool is_manual, bool is_due)
    {
        if (is_manual && is_due)
            return $"Scheduled occurrence at {eventTime(job.next_run!.Value)} and manual request from {eventTime(job.run_requested_utc!.Value)}.";
        if (is_manual)
            return $"Manual request from {eventTime(job.run_requested_utc!.Value)}.";
        return $"Scheduled occurrence at {eventTime(job.next_run!.Value)}.";
    }

    private void finish(TFwCron snapshot, string trigger, string status, string message, object? data, bool is_pause)
    {
        var now = DateTime.UtcNow;
        var result = resultJson(status, message, data);
        DateTime? skipped = null;
        var is_ambiguous_next = false;
        var finished = changeTask(snapshot.id, job =>
        {
            if (job.run_token != snapshot.run_token)
                return false;

            var fields = new FwDict
            {
                ["run_token"] = null,
                ["last_finished_utc"] = now,
                ["last_result"] = result
            };

            var next = job.next_run;
            if (isReport(job) && next <= now && (job.status == STATUS_ACTIVE || next > snapshot.last_started_utc))
            {
                skipped = next;
                next = nextOccurrence(job, now);
                is_ambiguous_next = isAmbiguousStorageTime(storageZone(), next);
                fields["next_run"] = is_ambiguous_next ? null : next;
            }

            if (is_pause || is_ambiguous_next)
            {
                fields["status"] = STATUS_INACTIVE;
            }
            else if (job.status == STATUS_ACTIVE && (!next.HasValue || job.end_date <= now))
            {
                fields["status"] = STATUS_COMPLETED;
                fields["next_run"] = null;
            }

            return writeClaim(job, fields);
        });

        if (!finished)
            return;

        if (skipped.HasValue)
            logEvent(snapshot.id, $"Skipped report occurrences during execution beginning at {eventTime(skipped.Value)}.");
        if (is_ambiguous_next)
            logEvent(snapshot.id, "Task paused before scheduling its next occurrence. " + STORAGE_FOLD_MESSAGE);
        logEvent(snapshot.id, $"Run finished at {eventTime(now)}. {trigger} {status}: {boundMessage(message)}");
    }

    /// <summary>
    /// Serializes changes to a current task. SQL Server takes an update lock on the row and SQLite's
    /// non-deferred provider transaction serializes writers. No handler or activity log runs inside this transaction.
    /// </summary>
    private bool changeTask(int id, Func<TFwCron, bool> change)
    {
        var table = db.dbtype switch
        {
            DB.DBTYPE_SQLSRV => $"{qTable()} WITH (UPDLOCK)",
            DB.DBTYPE_SQLITE => qTable(),
            _ => throw new NotSupportedException("Scheduled tasks require SQL Server or SQLite.")
        };

        bool changed;
        db.begin();
        try
        {
            var job = db.rowp<TFwCron>($@"
                SELECT *
                FROM {table}
                WHERE id = @id", DB.h("id", id));
            changed = job != null && change(job);
            if (changed)
                db.commit();
            else
                db.rollback();
        }
        catch
        {
            db.rollback();
            throw;
        }

        if (changed)
            removeCache(id);
        return changed;
    }

    private bool writeTask(int id, FwDict fields) =>
        fields.Count > 0 && db.update(table_name, fields, DB.h("id", id)) == 1;

    private bool writeDefinition(int id, FwDict fields)
    {
        setAuditFields(fields);
        return writeTask(id, fields);
    }

    private bool writeClaim(TFwCron job, FwDict fields, bool is_definition_change = false)
    {
        if (is_definition_change)
            setAuditFields(fields);

        return db.update(table_name, fields, new()
        {
            ["id"] = job.id,
            ["run_token"] = job.run_token
        }) == 1;
    }

    private void setAuditFields(FwDict fields)
    {
        fields["upd_time"] = DB.NOW;
        if (fw.isLogged)
            fields["upd_users_id"] = fw.userId;
    }

    /// <summary>Writes a bounded safe activity description without coupling logging failure to the task outcome.</summary>
    public void logEvent(int id, string message)
    {
        try
        {
            fw.logActivity(FwLogTypes.ICODE_CRON_EVENT, FwEntities.ICODE_CRON, id, boundMessage(message));
        }
        catch (Exception)
        {
            try
            {
                fw.logger(LogLevel.WARN, "Scheduled task activity could not be recorded. Task ID:", id);
            }
            catch (Exception)
            {
                // A logging extension cannot prevent a claim from executing or releasing.
            }
        }
    }

    private static string eventTime(DateTime date) => date.ToString("u", CultureInfo.InvariantCulture);

    private static bool isReport(TFwCron job) => job.icode.StartsWith(REPORT_PREFIX, StringComparison.OrdinalIgnoreCase);

    private static bool trySchedule(TFwCron job, out TimeZoneInfo zone)
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

    private static DateTime? nextOccurrence(TFwCron job, DateTime now) =>
        trySchedule(job, out var zone) ? calculateNextRun(job.cron, now, job.end_date, zone, job.start_date) : null;

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
    protected virtual FwDict? runJobAction(TFwCron job)
    {
        switch (job.icode)
        {
            case ICODE_EXAMPLE:
                Thread.Sleep(2000);
                return null;
            default:
                throw new FwCronPauseException("Unknown task handler code.");
        }
    }
}
