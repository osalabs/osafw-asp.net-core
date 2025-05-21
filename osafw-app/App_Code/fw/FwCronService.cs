using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

public class FwCronService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(1);

    private readonly IConfiguration _configuration;

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
    }

    private void ProcessJobs(CancellationToken ct)
    {
        try
        {
            using var fw = FW.initOffline(_configuration);
            var model = fw.model<FwCron>();
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

                    // Uncomment to log the errors in activity log
                    // fw.logActivity(FwLogTypes.ICODE_EXECUTED, FwEntities.ICODE_CRON, job.id, ex.Message);
                }
            }

            fw.endRequest();
        }
        catch (Exception) { }
    }
}
