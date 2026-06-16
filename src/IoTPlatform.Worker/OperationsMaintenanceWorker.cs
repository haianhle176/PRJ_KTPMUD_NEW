using IoTPlatform.Infrastructure.Operations;

namespace IoTPlatform.Worker;

public sealed class OperationsMaintenanceWorker(
    ILogger<OperationsMaintenanceWorker> logger,
    IOperationsMaintenanceRepository repository) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var aggregateChanges = await repository.AggregateTelemetryHourlyAsync(stoppingToken);
                var deletedRows = await repository.DeleteExpiredOperationalDataAsync(stoppingToken);
                logger.LogInformation(
                    "Operations maintenance completed. Aggregate changes: {AggregateChanges}, deleted rows: {DeletedRows}.",
                    aggregateChanges,
                    deletedRows);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Operations maintenance failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
