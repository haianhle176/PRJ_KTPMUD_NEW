namespace IoTPlatform.Application.Devices;

public interface IDeviceClaimRepository
{
    Task<DeviceSummaryDto?> ClaimAsync(Guid userId, string pairingCodeHash, string displayName, string? category, CancellationToken cancellationToken = default);
}
