using IoTPlatform.Application.Audit;

namespace IoTPlatform.Application.Devices;

public sealed class DeviceManagementService(IDeviceManagementRepository repository, IAuditLogRepository? auditLogRepository = null) : IDeviceManagementService
{
    public Task<IReadOnlyList<DeviceSummaryDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        return repository.ListAsync(userId, cancellationToken);
    }

    public async Task<DeviceSummaryDto> GetAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        return await repository.GetAsync(userId, deviceId, cancellationToken) ?? throw new DeviceNotFoundException();
    }

    public async Task<DeviceSummaryDto> UpdateAsync(Guid userId, Guid deviceId, UpdateDeviceRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("Display name is required.", nameof(request));
        }

        var updated = await repository.UpdateAsync(userId, deviceId, request.DisplayName.Trim(), request.Category?.Trim(), cancellationToken)
            ?? throw new DeviceNotFoundException();
        await WriteAuditAsync(userId, "device.update", "device", updated.Id.ToString(), cancellationToken);
        return updated;
    }

    public async Task UnclaimAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);

        if (!await repository.UnclaimAsync(userId, deviceId, cancellationToken))
        {
            throw new DeviceNotFoundException();
        }

        await WriteAuditAsync(userId, "device.unclaim", "device", deviceId.ToString(), cancellationToken);
    }

    private Task WriteAuditAsync(Guid userId, string action, string entityType, string entityId, CancellationToken cancellationToken = default) =>
        auditLogRepository?.WriteAsync(userId, action, entityType, entityId, cancellationToken: cancellationToken) ?? Task.CompletedTask;

    private static void ValidateUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }
    }
}
