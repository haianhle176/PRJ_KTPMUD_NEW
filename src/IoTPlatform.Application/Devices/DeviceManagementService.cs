using IoTPlatform.Application.Audit;

namespace IoTPlatform.Application.Devices;

public sealed class DeviceManagementService(IDeviceManagementRepository repository, IAuditLogRepository? auditLogRepository = null) : IDeviceManagementService
{
    public Task<IReadOnlyList<DeviceSummaryDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        return repository.ListAsync(userId, cancellationToken);
    }

    public Task<IReadOnlyList<AvailableDeviceDto>> ListAvailableAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        return repository.ListAvailableAsync(userId, cancellationToken);
    }

    public async Task<DeviceSummaryDto> ProvisionAsync(Guid userId, ProvisionDeviceRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        var normalizedRequest = NormalizeProvisionRequest(request);
        ValidateProvisionRequest(normalizedRequest);

        var provisioned = await repository.ProvisionAsync(userId, normalizedRequest, cancellationToken)
            ?? throw new DeviceAlreadyClaimedException();
        await WriteAuditAsync(userId, "device.provision", "device", provisioned.Id.ToString(), cancellationToken);
        return provisioned;
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

    public async Task<IReadOnlyList<DeviceCapabilityDto>> ListCapabilitiesAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        return await repository.ListCapabilitiesAsync(userId, deviceId, cancellationToken) ?? throw new DeviceNotFoundException();
    }

    private Task WriteAuditAsync(Guid userId, string action, string entityType, string entityId, CancellationToken cancellationToken = default) =>
        auditLogRepository?.WriteAsync(userId, action, entityType, entityId, cancellationToken: cancellationToken) ?? Task.CompletedTask;

    private static void ValidateProvisionRequest(ProvisionDeviceRequestDto request)
    {
        if (!Domain.Devices.DeviceSerial.IsValid(request.Serial))
        {
            throw new ArgumentException("Invalid device serial.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("Display name is required.", nameof(request));
        }

        if (request.ManifestVersion < 1)
        {
            throw new ArgumentException("Manifest version must be positive.", nameof(request));
        }

        if (request.Capabilities.Count == 0)
        {
            throw new ArgumentException("At least one capability is required.", nameof(request));
        }

        var definitions = request.Capabilities.Select(ToDefinition).ToArray();
        if (definitions.Select(definition => definition.Key).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
        {
            throw new ArgumentException("Capability keys must be unique within a device.", nameof(request));
        }
    }

    private static ProvisionDeviceRequestDto NormalizeProvisionRequest(ProvisionDeviceRequestDto request) => request with
    {
        Serial = NormalizeString(request.Serial),
        DisplayName = NormalizeString(request.DisplayName),
        Category = request.Category?.Trim(),
        DeviceType = NormalizeDeviceType(request.DeviceType),
        Capabilities = (request.Capabilities ?? [])
            .Select(capability => capability with
            {
                Key = NormalizeString(capability.Key),
                DisplayName = NormalizeString(capability.DisplayName),
                Kind = NormalizeString(capability.Kind).ToLowerInvariant(),
                DataType = NormalizeString(capability.DataType).ToLowerInvariant(),
                Unit = capability.Unit?.Trim(),
                SupportedCommands = capability.SupportedCommands ?? []
            })
            .ToArray()
    };

    private static Domain.Capabilities.CapabilityDefinition ToDefinition(ProvisionDeviceCapabilityDto capability) =>
        new Domain.Messaging.CapabilityManifestItem(
            capability.Key,
            capability.DisplayName,
            capability.Kind,
            capability.DataType,
            capability.Unit,
            capability.MinValue,
            capability.MaxValue,
            capability.IsWritable,
            capability.SupportedCommands).ToDefinition();

    private static string NormalizeDeviceType(string? deviceType)
    {
        var normalized = string.IsNullOrWhiteSpace(deviceType) ? "custom" : deviceType.Trim().ToLowerInvariant();
        return normalized.Length > 64 ? normalized[..64] : normalized;
    }

    private static string NormalizeString(string? value) => value?.Trim() ?? "";

    private static void ValidateUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }
    }
}
