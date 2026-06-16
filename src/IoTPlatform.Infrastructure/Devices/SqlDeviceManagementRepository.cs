using Dapper;
using IoTPlatform.Application.Devices;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Devices;

public sealed class SqlDeviceManagementRepository(IDbConnectionFactory connectionFactory) : IDeviceManagementRepository
{
    public async Task<IReadOnlyList<DeviceSummaryDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var devices = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT Id, Serial, DisplayName, Category, Status, ManifestVersion, LastSeenAtUtc, ClaimedAtUtc
            FROM dbo.Devices
            WHERE OwnerUserId = @UserId
            ORDER BY DisplayName, Serial;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
        return devices.Select(ToSummary).ToArray();
    }

    public async Task<DeviceSummaryDto?> GetAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            """
            SELECT Id, Serial, DisplayName, Category, Status, ManifestVersion, LastSeenAtUtc, ClaimedAtUtc
            FROM dbo.Devices
            WHERE Id = @DeviceId AND OwnerUserId = @UserId;
            """,
            new { DeviceId = deviceId, UserId = userId },
            cancellationToken: cancellationToken));
        return row is null ? null : ToSummary(row);
    }

    public async Task<DeviceSummaryDto?> UpdateAsync(Guid userId, Guid deviceId, string displayName, string? category, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            """
            UPDATE dbo.Devices
            SET DisplayName = @DisplayName, Category = @Category, UpdatedAtUtc = SYSUTCDATETIME()
            OUTPUT inserted.Id, inserted.Serial, inserted.DisplayName, inserted.Category, inserted.Status, inserted.ManifestVersion, inserted.LastSeenAtUtc, inserted.ClaimedAtUtc
            WHERE Id = @DeviceId AND OwnerUserId = @UserId;
            """,
            new { DeviceId = deviceId, UserId = userId, DisplayName = displayName, Category = category },
            cancellationToken: cancellationToken));
        return row is null ? null : ToSummary(row);
    }

    public async Task<bool> UnclaimAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.Devices
            SET OwnerUserId = NULL,
                DisplayName = NULL,
                Category = NULL,
                ClaimedAtUtc = NULL,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE Id = @DeviceId AND OwnerUserId = @UserId;
            """,
            new { DeviceId = deviceId, UserId = userId },
            cancellationToken: cancellationToken));
        return affected == 1;
    }

    private static DeviceSummaryDto ToSummary(dynamic row)
    {
        DateTime? lastSeenAtUtc = row.LastSeenAtUtc;
        DateTime? claimedAtUtc = row.ClaimedAtUtc;

        return new DeviceSummaryDto(
            row.Id,
            row.Serial,
            row.DisplayName,
            row.Category,
            row.Status,
            row.ManifestVersion,
            lastSeenAtUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(lastSeenAtUtc.Value, DateTimeKind.Utc)),
            claimedAtUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(claimedAtUtc.Value, DateTimeKind.Utc)));
    }
}
