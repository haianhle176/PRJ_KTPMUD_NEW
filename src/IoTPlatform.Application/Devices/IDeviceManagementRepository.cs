namespace IoTPlatform.Application.Devices;

public interface IDeviceManagementRepository
{
    Task<IReadOnlyList<DeviceSummaryDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<DeviceSummaryDto?> GetAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);

    Task<DeviceSummaryDto?> UpdateAsync(Guid userId, Guid deviceId, string displayName, string? category, CancellationToken cancellationToken = default);

    Task<bool> UnclaimAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceCapabilityDto>?> ListCapabilitiesAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);
}
