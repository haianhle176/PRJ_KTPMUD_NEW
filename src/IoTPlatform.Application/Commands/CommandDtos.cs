using System.Text.Json;

namespace IoTPlatform.Application.Commands;

public sealed record CreateCommandRequestDto(
    Guid DeviceId,
    string CapabilityKey,
    string CommandName,
    JsonElement DesiredValue);

public sealed record CommandDto(
    Guid Id,
    Guid DeviceId,
    string Serial,
    string CapabilityKey,
    string CommandName,
    string DesiredValueType,
    decimal? DesiredNumberValue,
    bool? DesiredBooleanValue,
    string? DesiredStringValue,
    string Source,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string? FailureCode,
    string? FailureMessage);
