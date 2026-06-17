using IoTPlatform.Infrastructure.Automation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IoTPlatform.Worker;

public sealed class ScheduledAutomationWorker(
    ILogger<ScheduledAutomationWorker> logger,
    IScheduledAutomationRepository repository) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var triggered = await repository.TriggerDueScheduleOnlyRulesAsync(DateTimeOffset.UtcNow, stoppingToken);
                if (triggered > 0)
                {
                    logger.LogInformation("Scheduled automation triggered {TriggeredCount} rule(s).", triggered);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled automation check failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
