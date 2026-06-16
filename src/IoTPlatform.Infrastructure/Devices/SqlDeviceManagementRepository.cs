using Dapper;
using System.Text.Json;
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

    public async Task<IReadOnlyList<AvailableDeviceDto>> ListAvailableAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var devices = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT
                d.Id AS DeviceId,
                d.Serial,
                d.Status,
                d.ManifestVersion,
                d.LastSeenAtUtc,
                pc.ExpiresAtUtc AS PairingCodeExpiresAtUtc,
                SUM(CASE WHEN c.Kind = 'sensor' AND c.IsActive = 1 THEN 1 ELSE 0 END) AS SensorCount,
                SUM(CASE WHEN c.Kind = 'actuator' AND c.IsActive = 1 THEN 1 ELSE 0 END) AS ActuatorCount
            FROM dbo.Devices d
            CROSS APPLY (
                SELECT TOP (1) ExpiresAtUtc
                FROM dbo.PairingCodes pc
                WHERE pc.DeviceId = d.Id
                  AND pc.UsedAtUtc IS NULL
                  AND pc.ExpiresAtUtc > SYSUTCDATETIME()
                ORDER BY pc.ExpiresAtUtc DESC, pc.CreatedAtUtc DESC
            ) pc
            LEFT JOIN dbo.Capabilities c ON c.DeviceId = d.Id AND c.IsActive = 1
            WHERE d.OwnerUserId IS NULL
            GROUP BY d.Id, d.Serial, d.Status, d.ManifestVersion, d.LastSeenAtUtc, pc.ExpiresAtUtc
            ORDER BY CASE WHEN d.Status = 'online' THEN 0 ELSE 1 END,
                     d.LastSeenAtUtc DESC,
                     d.Serial ASC;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
        return devices.Select(ToAvailable).ToArray();
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

    public async Task<IReadOnlyList<DeviceCapabilityDto>?> ListCapabilitiesAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.Devices WHERE Id = @DeviceId AND OwnerUserId = @UserId;",
            new { DeviceId = deviceId, UserId = userId },
            cancellationToken: cancellationToken));
        if (exists == 0)
        {
            return null;
        }

        var rows = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT Id, DeviceId, [Key], DisplayName, Kind, DataType, Unit, MinValue, MaxValue, IsWritable, SupportedCommandsJson
            FROM dbo.Capabilities
            WHERE DeviceId = @DeviceId AND IsActive = 1
            ORDER BY Kind, DisplayName, [Key];
            """,
            new { DeviceId = deviceId },
            cancellationToken: cancellationToken));
        return rows.Select(ToCapability).ToArray();
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

    private static AvailableDeviceDto ToAvailable(dynamic row)
    {
        DateTime? lastSeenAtUtc = row.LastSeenAtUtc;
        DateTime pairingCodeExpiresAtUtc = row.PairingCodeExpiresAtUtc;

        return new AvailableDeviceDto(
            row.DeviceId,
            row.Serial,
            row.Status,
            row.ManifestVersion,
            lastSeenAtUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(lastSeenAtUtc.Value, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(pairingCodeExpiresAtUtc, DateTimeKind.Utc)),
            row.SensorCount,
            row.ActuatorCount);
    }

    private static DeviceCapabilityDto ToCapability(dynamic row)
    {
        string supportedCommandsJson = row.SupportedCommandsJson;
        return new DeviceCapabilityDto(
            row.Id,
            row.DeviceId,
            row.Key,
            row.DisplayName,
            row.Kind,
            row.DataType,
            row.Unit,
            row.MinValue,
            row.MaxValue,
            row.IsWritable,
            JsonSerializer.Deserialize<string[]>(supportedCommandsJson) ?? []);
    }
}
