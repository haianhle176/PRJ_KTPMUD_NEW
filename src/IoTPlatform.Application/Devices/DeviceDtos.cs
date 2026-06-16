namespace IoTPlatform.Application.Devices;

public sealed record ClaimDeviceRequestDto(string PairingCode, string DisplayName, string? Category);

public sealed record DeviceSummaryDto(
    Guid Id,
    string Serial,
    string DisplayName,
    string? Category,
    string Status,
    int ManifestVersion,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset? ClaimedAtUtc);

public sealed record UpdateDeviceRequestDto(string DisplayName, string? Category);
