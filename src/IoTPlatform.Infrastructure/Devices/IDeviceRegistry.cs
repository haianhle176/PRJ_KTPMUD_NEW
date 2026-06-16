using IoTPlatform.Domain.Messaging;

namespace IoTPlatform.Infrastructure.Devices;

public interface IDeviceRegistry
{
    Task<DeviceRegistrationResult> RegisterManifestAsync(
        DeviceManifestMessage manifest,
        CancellationToken cancellationToken = default);

    Task UpdatePresenceAsync(
        DevicePresenceMessage presence,
        CancellationToken cancellationToken = default);
}
