using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

public class FwCronService : BackgroundService
{
    protected virtual TimeSpan PollingInterval => TimeSpan.FromMinutes(1);
    private readonly IConfiguration _configuration;
    private readonly ILogger<FwCronService> _logger;

    public FwCronService(IConfiguration configuration) : this(configuration, NullLogger<FwCronService>.Instance)
    {
    }

    public FwCronService(IConfiguration configuration, ILogger<FwCronService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ProcessJobs(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("Scheduled task polling failed ({ErrorType}).", ex.GetType().Name);
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        stoppingToken.ThrowIfCancellationRequested();
    }

    protected virtual void ProcessJobs(CancellationToken ct)
    {
        List<TFwCron> jobs;
        using (var fw = FW.initOffline(_configuration))
        {
            jobs = fw.model<FwCron>().listDueJobs();
            fw.endRequest();
        }

        foreach (var job in jobs)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                using var fw = FW.initOffline(_configuration);
                if (ct.IsCancellationRequested)
                    break;

                fw.model<FwCron>().runJob(job);
                fw.endRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError("Scheduled task {TaskId} could not be processed ({ErrorType}).", job.id, ex.GetType().Name);
            }
        }
    }
}
