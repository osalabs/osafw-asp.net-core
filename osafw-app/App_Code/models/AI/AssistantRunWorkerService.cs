using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

public sealed class AssistantRunWorkerService : BackgroundService
{
    private static readonly TimeSpan QueueRecoveryProbeInterval = TimeSpan.FromMinutes(5);

    private readonly IConfiguration configuration;
    private readonly ILoggerFactory loggerFactory;
    private readonly string workerId = Environment.MachineName + ":" + Guid.NewGuid().ToString("N")[..8];

    public AssistantRunWorkerService(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        this.configuration = configuration;
        this.loggerFactory = loggerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool shouldProbeQueue = true;
        bool shouldRecoverStaleQueue = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!shouldProbeQueue)
                shouldRecoverStaleQueue = !await AssistantRuns.WaitForQueuedRunAsync(QueueRecoveryProbeInterval, stoppingToken).ConfigureAwait(false);

            try
            {
                bool processed;
                do
                {
                    var processor = new AssistantRunProcessor(configuration, loggerFactory);
                    processed = await processor.ProcessNextQueuedSourceAsync(workerId, stoppingToken, shouldRecoverStaleQueue).ConfigureAwait(false);
                    if (!processed)
                        processed = await processor.ProcessNextQueuedRunAsync(workerId, stoppingToken, shouldRecoverStaleQueue).ConfigureAwait(false);
                    shouldRecoverStaleQueue = false;
                }
                while (processed && !stoppingToken.IsCancellationRequested);

                shouldProbeQueue = false;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                shouldProbeQueue = true;
                shouldRecoverStaleQueue = true;
                await Task.Delay(5000, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
