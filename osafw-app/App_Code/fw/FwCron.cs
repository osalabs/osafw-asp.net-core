// Cron Job Model and Logic
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

namespace osafw;

using Cronos;
using System;
using System.Threading;

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

    public int status { get; set; }
    public DateTime add_time { get; set; }
    public int add_users_id { get; set; }
    public DateTime upd_time { get; set; }
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

    /// <summary>
    /// Executes the logic associated with the specified job based on its ICode.
    /// If this is the first run (start_date is null), the job executes immediately,
    /// and the start_date is initialized to the current UTC time.
    /// Handles optional logging of execution and calculates the next run time based on the CRON expression.
    /// If no future run is scheduled, the job is marked as completed.
    /// </summary>
    public void runJob(TFwCron job)
    {
        // If start_date is not set, initialize the schedule and wait for the first matching CRON occurrence.
        if (!job.start_date.HasValue)
        {
            job.start_date = DateTime.UtcNow;
            update(job.id, DB.h("start_date", job.start_date));

            updateNextRun(job, job.start_date.Value);

            return;
        }

        var run_started_utc = DateTime.UtcNow;

        // Execute logic based on job's ICode
        runJobAction(job);

        // Advance the schedule from when this run started so long-running jobs do not skip the next slot.
        updateNextRun(job, run_started_utc);
    }

    private void updateNextRun(TFwCron job, DateTime from_date)
    {
        job.next_run = calculateNextRun(job.cron, from_date, job.end_date);
        update(job.id, DB.h("next_run", job.next_run));

        if (!job.next_run.HasValue)
        {
            job.status = STATUS_COMPLETED;
            update(job.id, DB.h("status", job.status));
        }
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
    /// <exception cref="Exception">Thrown if the ICode is unknown.</exception>
    private void runJobAction(TFwCron job)
    {
        switch (job.icode)
        {
            case ICODE_EXAMPLE:
                // Simulate work
                Thread.Sleep(2000);

                break;

            default:
                throw new Exception($"Unknown job code: {job.icode}");
        }

        // Uncomment to enable tracking of successful executions
        // fw.logActivity(FwLogTypes.ICODE_EXECUTED, FwEntities.ICODE_CRON, job.id);
    }
}
