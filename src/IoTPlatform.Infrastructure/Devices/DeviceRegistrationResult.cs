namespace IoTPlatform.Infrastructure.Devices;

public sealed record DeviceRegistrationResult(Guid DeviceId, string? PairingCode, DateTimeOffset? PairingCodeExpiresAtUtc);
