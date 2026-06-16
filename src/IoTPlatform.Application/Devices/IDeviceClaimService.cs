namespace IoTPlatform.Application.Devices;

public interface IDeviceClaimService
{
    Task<DeviceSummaryDto> ClaimAsync(Guid userId, ClaimDeviceRequestDto request, CancellationToken cancellationToken = default);
}
