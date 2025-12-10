// Cron Job Model and Logic
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System.Collections.Generic;

namespace osafw;

using Cronos;
using System;
using System.Linq;
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
        // If start_date is not set, treat this as the first execution and run immediately.
        // Also, record the current UTC time as the start_date for future CRON evaluations.
        if (!job.start_date.HasValue)
        {
            job.start_date = DateTime.UtcNow;
            update(job.id, DB.h("start_date", job.start_date));
        }

        // Execute logic based on job's ICode
        runJobAction(job);

        // Calculate the next run time using the CRON expression and start_date
        job.next_run = calculateNextRun(job.cron, job.start_date.Value, job.end_date);
        update(job.id, DB.h("next_run", job.next_run));

        // If no next occurrence is available, the schedule is considered complete
        if (!job.next_run.HasValue)
        {
            update(job.id, DB.h("status", STATUS_COMPLETED));
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
    /// Calculates the next run time for a job based on the given CRON expression and a start point.
    /// Determines the most recent valid run within the time window, then calculates the next occurrence.
    /// If an end_date is provided, ensures the schedule does not exceed it.
    /// Returns null if the CRON is invalid, the schedule is expired, or no future runs exist.
    /// </summary>
    /// <param name="cron">The CRON expression defining the job schedule.</param>
    /// <param name="start_date">The base date from which to start evaluating the CRON schedule.</param>
    /// <param name="end_date">Optional end date beyond which no runs should occur.</param>
    /// <returns>The next scheduled DateTime in UTC, or null if none exists.</returns>
    private static DateTime? calculateNextRun(string cron, DateTime start_date, DateTime? end_date)
    {
        // Try parsing the CRON expression
        if (!CronExpression.TryParse(cron, out var cron_expression))
        {
            return null; // Invalid CRON syntax
        }

        // Ensure all dates are treated as UTC
        var from_date_utc = DateTime.SpecifyKind(start_date, DateTimeKind.Utc);
        var to_date_utc = end_date.HasValue
            ? DateTime.SpecifyKind(end_date.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        // Get all CRON occurrences between start and now (or end_date if set)
        var occurrences = cron_expression.GetOccurrences(
            fromUtc: from_date_utc,
            toUtc: to_date_utc,
            zone: TimeZoneInfo.Utc,
            fromInclusive: true,
            toInclusive: false);

        // Get the last valid occurrence (if any) within that window
        DateTime? last = null;
        var lastOcc = occurrences.LastOrDefault();
        if (lastOcc != default)
            last = DateTime.SpecifyKind(lastOcc, DateTimeKind.Utc);

        // Get the next scheduled time after the last known run
        var next_run = cron_expression.GetNextOccurrence(
            fromUtc: last ?? from_date_utc, //fallback to job's start point
            zone: TimeZoneInfo.Utc);

        // Enforce the end_date constraint
        if (end_date.HasValue && next_run.HasValue && next_run > end_date)
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
