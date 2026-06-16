namespace IoTPlatform.Application.Telemetry;

public interface ITelemetryQueryRepository
{
    Task<IReadOnlyList<TelemetryValueDto>> GetLatestValuesAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<TelemetryHistoryItemDto> Items, int TotalCount)> GetHistoryAsync(
        Guid userId,
        Guid deviceId,
        string? capabilityKey,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
