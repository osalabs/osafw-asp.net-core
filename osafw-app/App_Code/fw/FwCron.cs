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
    public DateTime? last_run_utc { get; set; }
    public DateTime? next_run_utc { get; set; }
    public DateTime? start_date_utc { get; set; }
    public DateTime? end_date_utc { get; set; }

    public bool is_log_run { get; set; }
    public bool is_log_run_result { get; set; }

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
    /// Acquire job run lock
    /// </summary>
    /// <param name="id">Job id</param>
    /// <returns>True if there the lock is successfull</returns>
    public bool setIsRunning(int id)
    {
        // DB atomic operation for concurrent safety check: update and check rows affected
        // (multi-instance application, manual run + background service, simultaneous manual runs)
        var rows_affected = db.update(table_name,
            DB.h("is_running", 1),
            DB.h("id", id, "is_running", 0)
        );

        var success = rows_affected > 0;

        // cleanup cache if updated
        if (success)
            removeCache(id);

        return success;
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
    /// If this is the first run (next_run_utc is null), then calculate the next_run_utc and return early for the next Cron Service execution
    /// </summary>
    public void runJob(TFwCron job, bool is_manual_run = false)
    {
        // If manual run, run immediately and return early
        if (is_manual_run)
        {
            runJobAction(job, is_manual_run);
            return;
        }

        // If next_run_utc is not set, it's the first run; calculate the next_run_utc and return early
        if (!job.next_run_utc.HasValue)
        {
            updateNextRun(job);
            return;
        }

        // Execute logic based on job's ICode
        runJobAction(job, is_manual_run);
    }

    /// <summary>
    /// If the from_date is not passed, set it to the start_date_utc if it's greater than the current UTC, or set it to the current UTC otherwise.
    /// Calculates and updates the next run time based on the CRON expression.
    /// If no future run is scheduled, the job is marked as completed.
    /// </summary>
    public void updateNextRun(TFwCron job, DateTime? from_date = null)
    {
        if (from_date == null)
            from_date = job.start_date_utc.HasValue && job.start_date_utc.Value > DateTime.UtcNow
                        ? job.start_date_utc.Value
                        : DateTime.UtcNow;

        job.next_run_utc = calculateNextRun(job.cron, from_date.Value, job.end_date_utc);
        update(job.id, DB.h("next_run_utc", job.next_run_utc));

        if (!job.next_run_utc.HasValue)
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
    /// - is_running = 0 (not running)
    /// - next_run_utc is NULL (never scheduled) or next_run_utc <= current UTC time
    /// - start_date_utc is NULL or less than or equal to the current UTC time
    /// - end_date_utc is NULL or greater than the current UTC time
    /// </summary>
    public List<TFwCron> listDueJobs()
    {
        string sql = $@"
            SELECT *
            FROM {db.qid(table_name)}
            WHERE
                status = @status
                AND is_running = 0
                AND (next_run_utc IS NULL OR next_run_utc <= @now)
                AND (start_date_utc IS NULL OR start_date_utc <= @now)
                AND (end_date_utc IS NULL OR end_date > @now)
            ORDER BY next_run_utc";

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
    /// If the end date is provided, ensures the schedule does not exceed it.
    /// Returns null if the CRON is invalid, the schedule is expired, or no future runs exist.
    /// </summary>
    /// <param name="cron">The CRON expression defining the job schedule.</param>
    /// <param name="from_date">The reference date after which the next occurrence is calculated.</param>
    /// <param name="to_date">Optional end date beyond which no runs should occur.</param>
    /// <returns>The next scheduled DateTime in UTC, or null if none exists.</returns>
    private static DateTime? calculateNextRun(string cron, DateTime from_date, DateTime? to_date)
    {
        // Try parsing the CRON expression
        if (!CronExpression.TryParse(cron, out var cron_expression))
        {
            return null; // Invalid CRON syntax
        }

        // Ensure all dates are treated as UTC
        var from_date_utc = DateTime.SpecifyKind(from_date, DateTimeKind.Utc);
        DateTime? to_date_utc = to_date.HasValue
            ? DateTime.SpecifyKind(to_date.Value, DateTimeKind.Utc)
            : null;

        if (to_date_utc.HasValue && to_date_utc.Value <= from_date_utc)
        {
            return null;
        }

        // Get the next scheduled time after the current reference point
        var next_run_utc = cron_expression.GetNextOccurrence(
            fromUtc: from_date_utc,
            zone: TimeZoneInfo.Utc);

        // Enforce the end date constraint
        if (to_date_utc.HasValue && next_run_utc.HasValue && next_run_utc > to_date_utc.Value)
        {
            return null;
        }

        return next_run_utc;
    }

    /// <summary>
    /// Executes the logic associated with the specified ICode.
    /// </summary>
    /// <param name="job">The job to execute.</param>
    /// <param name="is_manual_run">Manual run flag.</param>
    /// <exception cref="Exception">Thrown if the ICode is unknown.</exception>
    private void runJobAction(TFwCron job, bool is_manual_run)
    {
        // Check for simultaneous runs (multi-instance application or background + manual run)
        var is_already_running = setIsRunning(job.id) == false;

        // Exception on manual run
        if (is_already_running && is_manual_run)
            throw new Exception($"The job is already running.");
        // Silent return otherwise (in multi-instance application environment, for example)
        else if (is_already_running)
            return;

        // JOB START
        var run_started_utc = DateTime.UtcNow;

        var log_type_start = is_manual_run ? FwLogTypes.ICODE_CRON_JOB_MANUAL_RUN_START : FwLogTypes.ICODE_CRON_JOB_RUN_START;
        var log_type_end = is_manual_run ? FwLogTypes.ICODE_CRON_JOB_MANUAL_RUN_END : FwLogTypes.ICODE_CRON_JOB_RUN_END;

        var success = true;
        // Wrap in try/finally to make sure the "is_running" flag is always reset
        try
        {
            // Track start
            if (job.is_log_run)
                fw.logActivity(log_type_start, FwEntities.ICODE_CRON, job.id);

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
            if (is_manual_run && job.is_log_run)
                fw.logActivity(FwLogTypes.ICODE_CRON_JOB_RUN_ERROR, FwEntities.ICODE_CRON, job.id, ex.Message);

            throw;
        }
        finally
        {
            // JOB END

            // Track end only if no exceptions
            if (success && job.is_log_run)
                fw.logActivity(log_type_end, FwEntities.ICODE_CRON, job.id);

            // Advance the schedule from when this run started so long-running jobs do not skip the next slot
            updateNextRun(job, run_started_utc);

            // Update the last run
            update(job.id, DB.h("last_run_utc", run_started_utc));

            // Release the job for the next run (reset the "is_running" flag)
            resetIsRunning(job.id);
        }
    }
}
