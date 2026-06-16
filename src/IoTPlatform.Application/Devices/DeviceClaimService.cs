using System.Security.Cryptography;
using System.Text;
using IoTPlatform.Application.Audit;

namespace IoTPlatform.Application.Devices;

public sealed class DeviceClaimService(IDeviceClaimRepository repository, IAuditLogRepository? auditLogRepository = null) : IDeviceClaimService
{
    public Task<DeviceSummaryDto> ClaimAsync(Guid userId, ClaimDeviceRequestDto request, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (request.PairingCode.Length != 8)
        {
            throw new InvalidPairingCodeException();
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("Display name is required.", nameof(request));
        }

        return ClaimInternalAsync(userId, request, cancellationToken);
    }

    private async Task<DeviceSummaryDto> ClaimInternalAsync(Guid userId, ClaimDeviceRequestDto request, CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.PairingCode.Trim().ToUpperInvariant())));
        var claimed = await repository.ClaimAsync(userId, hash, request.DisplayName.Trim(), request.Category?.Trim(), cancellationToken)
            ?? throw new InvalidPairingCodeException();
        await WriteAuditAsync(userId, "device.claim", "device", claimed.Id.ToString(), cancellationToken: cancellationToken);
        return claimed;
    }

    private Task WriteAuditAsync(Guid userId, string action, string entityType, string entityId, CancellationToken cancellationToken = default) =>
        auditLogRepository?.WriteAsync(userId, action, entityType, entityId, cancellationToken: cancellationToken) ?? Task.CompletedTask;
}
