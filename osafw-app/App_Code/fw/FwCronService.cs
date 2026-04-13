using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

public class FwCronService : BackgroundService
{
    protected virtual TimeSpan PollingInterval => TimeSpan.FromMinutes(1);
    private readonly IConfiguration _configuration;
    private static bool is_first_run = false;

    public FwCronService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ProcessJobs(stoppingToken);

            await Task.Delay(PollingInterval, stoppingToken);
        }

        stoppingToken.ThrowIfCancellationRequested();
    }

    protected virtual void ProcessJobs(CancellationToken ct)
    {
        try
        {
            using var fw = FW.initOffline(_configuration);
            var model = fw.model<FwCron>();

            // Check for the abnormally terminated jobs during the first app run and reset the "is_running" flag
            // TODO options:
            // 1. Implement jobs logic to resume operation if terminated abnormally.
            // 2. Do not reset the "is_running" flag, instead notify admin and show dashboard or notification message to review
            if (!is_first_run)
            {
                var jobs_running = model.listRunningJobs();

                foreach (var job in jobs_running)
                {
                    var err_msg = "Terminated abnormally. Resetting the \"Is Running\" flag.";
                    fw.logger(LogLevel.ERROR, "Cron Jobs first run. Abnormally terminated job detected:", job.id, ", error:", err_msg);

                    if (FwCron.IS_TRACK_JOB_RUN_IN_ACTIVITY_LOGS)
                        fw.logActivity(FwLogTypes.ICODE_CRON_JOB_RUN_ERROR, FwEntities.ICODE_CRON, job.id, err_msg);

                    model.resetIsRunning(job.id);
                }

                is_first_run = true;
            }

            var jobsToRun = model.listDueJobs();

            foreach (var job in jobsToRun)
            {
                try
                {
                    model.runJob(job);
                }
                catch (Exception ex)
                {
                    // Log the error to Sentry (or other error tracking systems) if available
                    fw.logger(LogLevel.ERROR, "Failed to execute job:", job.id, ", error:", ex.Message);
                    fw.logger(LogLevel.ERROR, ex.StackTrace);

                    if (FwCron.IS_TRACK_JOB_RUN_IN_ACTIVITY_LOGS)
                        fw.logActivity(FwLogTypes.ICODE_CRON_JOB_RUN_ERROR, FwEntities.ICODE_CRON, job.id, ex.Message);

                    // Reset the "is_running" flag in case of error
                    model.resetIsRunning(job.id);
                }
            }

            fw.endRequest();
        }
        catch (Exception) { }
    }
}
