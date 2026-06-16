namespace IoTPlatform.Application.Telemetry;

public sealed record TelemetryValueDto(
    Guid CapabilityId,
    string CapabilityKey,
    string DisplayName,
    string ValueType,
    decimal? NumberValue,
    bool? BooleanValue,
    string? StringValue,
    DateTimeOffset RecordedAtUtc);

public sealed record TelemetryHistoryItemDto(
    long Id,
    Guid CapabilityId,
    string CapabilityKey,
    string DisplayName,
    string ValueType,
    decimal? NumberValue,
    bool? BooleanValue,
    string? StringValue,
    DateTimeOffset RecordedAtUtc,
    DateTimeOffset ReceivedAtUtc);

public sealed record TelemetryHistoryPageDto(
    IReadOnlyList<TelemetryHistoryItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
