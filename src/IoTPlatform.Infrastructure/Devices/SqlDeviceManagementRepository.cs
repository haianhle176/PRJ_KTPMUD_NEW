using Dapper;
using System.Text.Json;
using IoTPlatform.Application.Devices;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Devices;

public sealed class SqlDeviceManagementRepository(
    IDbConnectionFactory connectionFactory,
    ISqlTransactionScopeFactory transactionScopeFactory) : IDeviceManagementRepository
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
                d.DeviceType,
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
              AND (d.AvailableToUserId IS NULL OR d.AvailableToUserId = @UserId)
              AND d.Status = 'online'
              AND d.LastSeenAtUtc >= DATEADD(second, -45, SYSUTCDATETIME())
            GROUP BY d.Id, d.Serial, d.DeviceType, d.Status, d.ManifestVersion, d.LastSeenAtUtc, pc.ExpiresAtUtc
            ORDER BY d.LastSeenAtUtc DESC,
                     d.Serial ASC;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
        return devices.Select(ToAvailable).ToArray();
    }

    public async Task<DeviceSummaryDto?> ProvisionAsync(Guid userId, ProvisionDeviceRequestDto request, CancellationToken cancellationToken = default)
    {
        await using var scope = await transactionScopeFactory.BeginAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var existing = await scope.Connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            """
            SELECT Id, OwnerUserId, AvailableToUserId, ManifestVersion
            FROM dbo.Devices WITH (UPDLOCK, HOLDLOCK)
            WHERE Serial = @Serial;
            """,
            new { request.Serial },
            scope.Transaction,
            cancellationToken: cancellationToken));

        Guid deviceId;
        if (existing is null)
        {
            deviceId = Guid.NewGuid();
            await scope.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT dbo.Devices
                    (Id, Serial, OwnerUserId, AvailableToUserId, DisplayName, Category, DeviceType, Status, ManifestVersion, ClaimedAtUtc, CreatedAtUtc, UpdatedAtUtc)
                VALUES
                    (@DeviceId, @Serial, NULL, @UserId, @DisplayName, @Category, @DeviceType, 'offline', @ManifestVersion, NULL, @Now, @Now);
                """,
                new
                {
                    DeviceId = deviceId,
                    request.Serial,
                    UserId = userId,
                    request.DisplayName,
                    request.Category,
                    request.DeviceType,
                    request.ManifestVersion,
                    Now = now
                },
                scope.Transaction,
                cancellationToken: cancellationToken));
        }
        else
        {
            Guid? ownerUserId = existing.OwnerUserId;
            if (ownerUserId is not null && ownerUserId != userId)
            {
                await scope.CommitAsync(cancellationToken);
                return null;
            }

            Guid? availableToUserId = existing.AvailableToUserId;
            if (ownerUserId is null && availableToUserId is not null && availableToUserId != userId)
            {
                await scope.CommitAsync(cancellationToken);
                return null;
            }

            deviceId = existing.Id;
            await scope.Connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.Devices
                SET AvailableToUserId = CASE WHEN OwnerUserId IS NULL THEN @UserId ELSE AvailableToUserId END,
                    DisplayName = @DisplayName,
                    Category = @Category,
                    DeviceType = @DeviceType,
                    ManifestVersion = CASE WHEN ManifestVersion > @ManifestVersion THEN ManifestVersion ELSE @ManifestVersion END,
                    UpdatedAtUtc = @Now
                WHERE Id = @DeviceId;
                """,
                new
                {
                    DeviceId = deviceId,
                    UserId = userId,
                    request.DisplayName,
                    request.Category,
                    request.DeviceType,
                    request.ManifestVersion,
                    Now = now
                },
                scope.Transaction,
                cancellationToken: cancellationToken));
        }

        await scope.Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.Capabilities SET IsActive = 0, UpdatedAtUtc = @Now WHERE DeviceId = @DeviceId;",
            new { DeviceId = deviceId, Now = now },
            scope.Transaction,
            cancellationToken: cancellationToken));

        foreach (var capability in request.Capabilities)
        {
            await scope.Connection.ExecuteAsync(new CommandDefinition(
                """
                MERGE dbo.Capabilities WITH (HOLDLOCK) AS target
                USING (SELECT @DeviceId AS DeviceId, @Key AS [Key]) AS source
                ON target.DeviceId = source.DeviceId AND target.[Key] = source.[Key]
                WHEN MATCHED THEN UPDATE SET
                    DisplayName = @DisplayName,
                    Kind = @Kind,
                    DataType = @DataType,
                    Unit = @Unit,
                    MinValue = @MinValue,
                    MaxValue = @MaxValue,
                    IsWritable = @IsWritable,
                    SupportedCommandsJson = @SupportedCommandsJson,
                    IsActive = 1,
                    UpdatedAtUtc = @Now
                WHEN NOT MATCHED THEN INSERT
                    (Id, DeviceId, [Key], DisplayName, Kind, DataType, Unit, MinValue, MaxValue, IsWritable, SupportedCommandsJson, IsActive, CreatedAtUtc, UpdatedAtUtc)
                VALUES
                    (@Id, @DeviceId, @Key, @DisplayName, @Kind, @DataType, @Unit, @MinValue, @MaxValue, @IsWritable, @SupportedCommandsJson, 1, @Now, @Now);
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    capability.Key,
                    capability.DisplayName,
                    capability.Kind,
                    capability.DataType,
                    capability.Unit,
                    capability.MinValue,
                    capability.MaxValue,
                    capability.IsWritable,
                    SupportedCommandsJson = JsonSerializer.Serialize(capability.SupportedCommands),
                    Now = now
                },
                scope.Transaction,
                cancellationToken: cancellationToken));
        }

        var row = await scope.Connection.QuerySingleAsync(new CommandDefinition(
            """
            SELECT Id, Serial, DisplayName, Category, Status, ManifestVersion, LastSeenAtUtc, ClaimedAtUtc
            FROM dbo.Devices
            WHERE Id = @DeviceId
              AND (OwnerUserId = @UserId OR AvailableToUserId = @UserId);
            """,
            new { DeviceId = deviceId, UserId = userId },
            scope.Transaction,
            cancellationToken: cancellationToken));

        await scope.CommitAsync(cancellationToken);
        return ToSummary(row);
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
                AvailableToUserId = NULL,
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
            row.DeviceType,
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
