using Dapper;
using IoTPlatform.Application.Devices;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Devices;

public sealed class SqlDeviceClaimRepository(ISqlTransactionScopeFactory transactionScopeFactory) : IDeviceClaimRepository
{
    public async Task<DeviceSummaryDto?> ClaimAsync(
        Guid userId,
        string pairingCodeHash,
        string displayName,
        string? category,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await transactionScopeFactory.BeginAsync(cancellationToken);
        var row = await scope.Connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            """
            SELECT d.Id, d.Serial, d.OwnerUserId, d.Status, d.ManifestVersion, d.LastSeenAtUtc, pc.Id AS PairingCodeId
            FROM dbo.PairingCodes pc WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.Devices d WITH (UPDLOCK, HOLDLOCK) ON d.Id = pc.DeviceId
            WHERE pc.CodeHash = @CodeHash
              AND pc.UsedAtUtc IS NULL
              AND pc.ExpiresAtUtc > SYSUTCDATETIME()
              AND (d.AvailableToUserId IS NULL OR d.AvailableToUserId = @UserId);
            """,
            new { CodeHash = pairingCodeHash, UserId = userId },
            scope.Transaction,
            cancellationToken: cancellationToken));

        if (row is null)
        {
            await scope.CommitAsync(cancellationToken);
            return null;
        }

        Guid deviceId = row.Id;
        string serial = row.Serial;
        Guid? ownerUserId = row.OwnerUserId;
        string status = row.Status;
        int manifestVersion = row.ManifestVersion;
        DateTime? lastSeenAtUtc = row.LastSeenAtUtc;
        Guid pairingCodeId = row.PairingCodeId;

        if (ownerUserId is not null && ownerUserId != userId)
        {
            throw new DeviceAlreadyClaimedException();
        }

        var now = DateTimeOffset.UtcNow;
        await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.Devices
            SET OwnerUserId = @UserId,
                AvailableToUserId = NULL,
                DisplayName = @DisplayName,
                Category = @Category,
                ClaimedAtUtc = COALESCE(ClaimedAtUtc, @Now),
                UpdatedAtUtc = @Now
            WHERE Id = @DeviceId;

            UPDATE dbo.PairingCodes
            SET UsedAtUtc = @Now
            WHERE Id = @PairingCodeId;
            """,
            new
            {
                UserId = userId,
                DisplayName = displayName,
                Category = category,
                Now = now,
                DeviceId = deviceId,
                PairingCodeId = pairingCodeId
            },
            scope.Transaction,
            cancellationToken: cancellationToken));

        await scope.CommitAsync(cancellationToken);
        return new DeviceSummaryDto(deviceId, serial, displayName, category, status, manifestVersion, lastSeenAtUtc, now);
    }
}
