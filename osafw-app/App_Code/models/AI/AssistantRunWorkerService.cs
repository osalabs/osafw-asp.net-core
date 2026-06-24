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
    private const int MAX_SOURCES_BEFORE_RUN_CHECK = 3;

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
        bool isQueueProbeDue = true;
        bool isStaleQueueRecoveryDue = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!isQueueProbeDue)
                isStaleQueueRecoveryDue = !await AssistantRuns.WaitForQueuedRunAsync(QueueRecoveryProbeInterval, stoppingToken).ConfigureAwait(false);

            try
            {
                bool isProcessed;
                int processedSourcesSinceRunCheck = 0;
                do
                {
                    var processor = new AssistantRunProcessor(configuration, loggerFactory);
                    isProcessed = false;
                    if (processedSourcesSinceRunCheck >= MAX_SOURCES_BEFORE_RUN_CHECK)
                    {
                        isProcessed = await processor.ProcessNextQueuedRunAsync(workerId, stoppingToken, isStaleQueueRecoveryDue).ConfigureAwait(false);
                        processedSourcesSinceRunCheck = 0;
                    }

                    if (!isProcessed)
                    {
                        isProcessed = await processor.ProcessNextQueuedSourceAsync(workerId, stoppingToken, isStaleQueueRecoveryDue).ConfigureAwait(false);
                        if (isProcessed)
                            processedSourcesSinceRunCheck++;
                    }

                    if (!isProcessed)
                    {
                        isProcessed = await processor.ProcessNextQueuedRunAsync(workerId, stoppingToken, isStaleQueueRecoveryDue).ConfigureAwait(false);
                        processedSourcesSinceRunCheck = 0;
                    }
                    isStaleQueueRecoveryDue = false;
                }
                while (isProcessed && !stoppingToken.IsCancellationRequested);

                isQueueProbeDue = false;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger<AssistantRunWorkerService>().LogWarning(
                    ex,
                    "Assistant worker {WorkerId} loop failed: {ExceptionType}: {Message}",
                    workerId,
                    ex.GetType().Name,
                    ex.Message);
                isQueueProbeDue = true;
                isStaleQueueRecoveryDue = true;
                await Task.Delay(5000, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
