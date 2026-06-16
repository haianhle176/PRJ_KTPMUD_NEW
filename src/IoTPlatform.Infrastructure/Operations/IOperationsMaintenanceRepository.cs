namespace IoTPlatform.Infrastructure.Operations;

public interface IOperationsMaintenanceRepository
{
    Task<int> AggregateTelemetryHourlyAsync(CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredOperationalDataAsync(CancellationToken cancellationToken = default);
}
