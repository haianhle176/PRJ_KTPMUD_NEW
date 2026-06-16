namespace IoTPlatform.Application.Telemetry;

public sealed class TelemetryQueryService(ITelemetryQueryRepository repository) : ITelemetryQueryService
{
    public Task<IReadOnlyList<TelemetryValueDto>> GetLatestValuesAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        ValidateUserAndDevice(userId, deviceId);
        return repository.GetLatestValuesAsync(userId, deviceId, cancellationToken);
    }

    public async Task<TelemetryHistoryPageDto> GetHistoryAsync(
        Guid userId,
        Guid deviceId,
        string? capabilityKey,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateUserAndDevice(userId, deviceId);

        if (page < 1)
        {
            throw new ArgumentException("Page must be greater than or equal to 1.", nameof(page));
        }

        if (pageSize is < 1 or > 200)
        {
            throw new ArgumentException("Page size must be between 1 and 200.", nameof(pageSize));
        }

        if (fromUtc is not null && toUtc is not null && fromUtc > toUtc)
        {
            throw new ArgumentException("From timestamp must be before to timestamp.", nameof(fromUtc));
        }

        var skip = (page - 1) * pageSize;
        var (items, totalCount) = await repository.GetHistoryAsync(
            userId,
            deviceId,
            string.IsNullOrWhiteSpace(capabilityKey) ? null : capabilityKey.Trim(),
            fromUtc,
            toUtc,
            skip,
            pageSize,
            cancellationToken);

        return new TelemetryHistoryPageDto(items, page, pageSize, totalCount);
    }

    private static void ValidateUserAndDevice(Guid userId, Guid deviceId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("Device id is required.", nameof(deviceId));
        }
    }
}
