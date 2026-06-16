namespace IoTPlatform.Application.Devices;

public interface IDeviceManagementService
{
    Task<IReadOnlyList<DeviceSummaryDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AvailableDeviceDto>> ListAvailableAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<DeviceSummaryDto> GetAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);

    Task<DeviceSummaryDto> UpdateAsync(Guid userId, Guid deviceId, UpdateDeviceRequestDto request, CancellationToken cancellationToken = default);

    Task UnclaimAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceCapabilityDto>> ListCapabilitiesAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);
}
