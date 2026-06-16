namespace IoTPlatform.Application.Telemetry;

public interface ITelemetryQueryService
{
    Task<IReadOnlyList<TelemetryValueDto>> GetLatestValuesAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);

    Task<TelemetryHistoryPageDto> GetHistoryAsync(
        Guid userId,
        Guid deviceId,
        string? capabilityKey,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
