// Cron Job Model and Logic
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

namespace osafw;

using Cronos;
using CronExpressionDescriptor;
using System;
using System.Collections.Generic;
using System.Threading;

public class TFwCronExpressionDescriptor
{
    public string cron { get; set; } = string.Empty;
    public string cron_human { get; set; } = string.Empty;
    public bool is_error { get; set; }
    public string error_msg { get; set; } = string.Empty;
}

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

    public bool is_running { get; set; }
    public int status { get; set; }
    public DateTime add_time { get; set; }
    public int add_users_id { get; set; }
    public DateTime? upd_time { get; set; }
    public int upd_users_id { get; set; }
}

public class FwCron : FwModel
{
    // IMPORTANT:
    // For every new job type:
    // 1. Define a constant here like: public const string ICODE_XYZ = "xyz";
    // 2. Add handling for it in FwCron.runJobAction(string icode)
    public const string ICODE_EXAMPLE = "example_sleep";

    public const int STATUS_COMPLETED = 20;

    // track job start, end and error events in FwActivityLogs
    // "activity_logs_id" can be passed to the job logic to add job specific details to the log, like summary, for example: "100 records processed. 5 users notified."
    public const bool IS_TRACK_JOB_RUN_IN_ACTIVITY_LOGS = false;

    public FwCron() : base()
    {
        table_name = "fwcron";

        is_log_changes = false; // do not log changes as cron service will update it frequently
    }

    public bool isValidCronExpression(string cron)
    {
        return CronExpression.TryParse(cron, out _);
    }

    public TFwCronExpressionDescriptor getCronExpressionDescriptor(string cron)
    {
        var result = new TFwCronExpressionDescriptor() { cron = cron };

        if (!Utils.isEmpty(cron))
        {
            try
            {
                result.is_error = false;
                result.cron_human = ExpressionDescriptor.GetDescription(cron);
            }
            catch (Exception ex)
            {
                result.is_error = true;
                result.error_msg = ex.Message;
            }
        }
        else
        {
            result.is_error = true;
            result.error_msg = "Empty Cron Expression";
        }

        return result;
    }

    public bool isRunning(int id)
    {
        return db.value(table_name, DB.h(field_id, id), "is_running").toBool();
    }

    /// <summary>
    /// Acquire job run lock, or throw an Exception
    /// </summary>
    /// <param name="id">Job id</param>
    /// <exception cref="Exception">Thrown when the job is already running</exception>
    public void setIsRunningOrFail(int id)
    {
        // concurrent safety check (manual run + background service, simultaneous manual runs, multiple app instances)
        // update and check rows affected
        var rows_affected = db.update(table_name,
            DB.h("is_running", 1),
            DB.h("id", id, "is_running", 0)
        );

        if (rows_affected == 0)
            throw new Exception($"The job is already running [id:{id}].");

        // cleanup cache after update
        removeCache(id);
    }

    public void resetIsRunning(int id)
    {
        update(id, DB.h("is_running", 0));
    }

    /// <summary>
    /// Get job record
    /// </summary>
    public TFwCron? oneJob(int id)
    {
        var job = db.row<TFwCron>(table_name, DB.h(field_id, id));

        return job.id == 0 ? null : job;
    }

    /// <summary>
    /// List running jobs
    /// </summary>
    public List<TFwCron> listRunningJobs()
    {
        return db.array<TFwCron>(table_name, DB.h("is_running", 1));
    }

    /// <summary>
    /// Executes the logic associated with the specified job based on its ICode.
    /// If this is the first run (next_run is null), then calculate the next_run and return early for the next Cron Service execution
    /// </summary>
    public void runJob(TFwCron job, bool is_manual_run = false)
    {
        // If manual run, run immediately and return early
        if (is_manual_run)
        {
            runJobAction(job, is_manual_run);
            return;
        }

        // If next_run is not set, it's the first run; calculate the next_run and return early
        if (!job.next_run.HasValue)
        {
            updateNextRun(job);
            return;
        }

        // Execute logic based on job's ICode
        runJobAction(job, is_manual_run);
    }

    /// <summary>
    /// If the from_date is not passed, set it to the start_date if it's greater than the current UTC, or set it to the current UTC otherwise.
    /// Calculates and updates the next run time based on the CRON expression.
    /// If no future run is scheduled, the job is marked as completed.
    /// <summary>
    public void updateNextRun(TFwCron job, DateTime? from_date = null)
    {
        if (from_date == null)
            from_date = job.start_date.HasValue && job.start_date.Value > DateTime.UtcNow
                        ? job.start_date.Value
                        : DateTime.UtcNow;

        job.next_run = calculateNextRun(job.cron, from_date.Value, job.end_date);
        update(job.id, DB.h("next_run", job.next_run));

        if (!job.next_run.HasValue)
        {
            job.status = STATUS_COMPLETED;
            update(job.id, DB.h("status", job.status));
        }
    }

    public void updateNextRun(int id, DateTime? from_date = null)
    {
        var job = oneJob(id);

        if (job != null)
            updateNextRun(job, from_date);
    }

    /// <summary>
    /// Returns all cron jobs that are due to run now.
    /// Criteria:
    /// - status = 0 (Active)
    /// - next_run is NULL (never scheduled) or next_run <= current UTC time
    /// - next_run is NULL (start immediately) or start_date <= current UTC time
    /// - end_date is NULL or greater than current UTC time
    /// </summary>
    public List<TFwCron> listDueJobs()
    {
        string sql = $@"
            SELECT *
            FROM {db.qid(table_name)}
            WHERE
                status = @status
                AND is_running = 0
                AND (next_run IS NULL OR next_run <= @now)
                AND (start_date IS NULL OR start_date <= @now)
                AND (end_date IS NULL OR end_date > @now)
            ORDER BY next_run";

        return db.arrayp<TFwCron>(sql,
            @params: new()
            {
                ["@status"] = STATUS_ACTIVE,
                ["@now"] = DateTime.UtcNow,
            });
    }

    /// <summary>
    /// Calculates the next run time for a job based on the given CRON expression and a reference point.
    /// Calculates the next occurrence after the supplied UTC reference time, respecting the schedule end bound.
    /// If an end_date is provided, ensures the schedule does not exceed it.
    /// Returns null if the CRON is invalid, the schedule is expired, or no future runs exist.
    /// </summary>
    /// <param name="cron">The CRON expression defining the job schedule.</param>
    /// <param name="from_date">The reference date after which the next occurrence is calculated.</param>
    /// <param name="end_date">Optional end date beyond which no runs should occur.</param>
    /// <returns>The next scheduled DateTime in UTC, or null if none exists.</returns>
    private static DateTime? calculateNextRun(string cron, DateTime from_date, DateTime? end_date)
    {
        // Try parsing the CRON expression
        if (!CronExpression.TryParse(cron, out var cron_expression))
        {
            return null; // Invalid CRON syntax
        }

        // Ensure all dates are treated as UTC
        var from_date_utc = DateTime.SpecifyKind(from_date, DateTimeKind.Utc);
        DateTime? end_date_utc = end_date.HasValue
            ? DateTime.SpecifyKind(end_date.Value, DateTimeKind.Utc)
            : null;

        if (end_date_utc.HasValue && end_date_utc.Value <= from_date_utc)
        {
            return null;
        }

        // Get the next scheduled time after the current reference point
        var next_run = cron_expression.GetNextOccurrence(
            fromUtc: from_date_utc,
            zone: TimeZoneInfo.Utc);

        // Enforce the end_date constraint
        if (end_date_utc.HasValue && next_run.HasValue && next_run > end_date_utc.Value)
        {
            return null;
        }

        return next_run;
    }

    /// <summary>
    /// Executes the logic associated with the specified ICode.
    /// </summary>
    /// <param name="job">The job to execute.</param>
    /// <param name="is_manual_run">Manual run flag.</param>
    /// <exception cref="Exception">Thrown if the ICode is unknown.</exception>
    private void runJobAction(TFwCron job, bool is_manual_run)
    {
        var run_started_utc = DateTime.UtcNow;

        var log_type_start = is_manual_run ? FwLogTypes.ICODE_CRON_JOB_MANUAL_RUN_START : FwLogTypes.ICODE_CRON_JOB_RUN_START;
        var log_type_end = is_manual_run ? FwLogTypes.ICODE_CRON_JOB_MANUAL_RUN_END : FwLogTypes.ICODE_CRON_JOB_RUN_END;

        setIsRunningOrFail(job.id);

        var success = true;
        // Wrap in try/finally to make sure the "is_running" flag is always reset
        try
        {
            // Track start
            var activity_logs_id = 0;
            if (IS_TRACK_JOB_RUN_IN_ACTIVITY_LOGS)
                activity_logs_id = fw.logActivity(log_type_start, FwEntities.ICODE_CRON, job.id);

            switch (job.icode)
            {
                case ICODE_EXAMPLE:
                    // Simulate work
                    Thread.Sleep(2000);

                    break;

                default:
                    // Set job status to Inactive if no logic defined
                    update(job.id, DB.h("status", STATUS_INACTIVE));
                    throw new Exception($"Unknown job code: \"{job.icode}\". No logic defined for the Job. Set status to \"Inactive\".");
            }
        }
        catch (Exception ex)
        {
            success = false;
            // Manual run needs explicit error logging here
            if (is_manual_run && FwCron.IS_TRACK_JOB_RUN_IN_ACTIVITY_LOGS)
                fw.logActivity(FwLogTypes.ICODE_CRON_JOB_RUN_ERROR, FwEntities.ICODE_CRON, job.id, ex.Message);

            throw;
        }
        finally
        {
            // Track end only if no exceptions
            if (success && IS_TRACK_JOB_RUN_IN_ACTIVITY_LOGS)
                fw.logActivity(log_type_end, FwEntities.ICODE_CRON, job.id);

            // Advance the schedule from when this run started so long-running jobs do not skip the next slot
            updateNextRun(job, run_started_utc);

            // Update the last run
            update(job.id, DB.h("last_run", run_started_utc));

            // Release the job for the next run (reset the "is_running" flag)
            resetIsRunning(job.id);
        }
    }
}
