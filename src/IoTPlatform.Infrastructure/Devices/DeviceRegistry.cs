using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Devices;

public sealed class DeviceRegistry(ISqlTransactionScopeFactory transactionScopeFactory) : IDeviceRegistry
{
    public async Task<DeviceRegistrationResult> RegisterManifestAsync(
        DeviceManifestMessage manifest,
        CancellationToken cancellationToken = default)
    {
        ValidateManifest(manifest);
        var definitions = manifest.Capabilities.Select(capability => capability.ToDefinition()).ToArray();

        if (definitions.Select(definition => definition.Key).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
        {
            throw new ArgumentException("Capability keys must be unique within a manifest.", nameof(manifest));
        }

        await using var scope = await transactionScopeFactory.BeginAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var existing = await scope.Connection.QuerySingleOrDefaultAsync<DeviceRow>(new CommandDefinition(
            "SELECT Id, ManifestVersion FROM dbo.Devices WITH (UPDLOCK, HOLDLOCK) WHERE Serial = @Serial;",
            new { manifest.Serial },
            scope.Transaction,
            cancellationToken: cancellationToken));

        var deviceId = existing?.Id ?? Guid.NewGuid();

        if (existing is null)
        {
            await scope.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT dbo.Devices (Id, Serial, Status, ManifestVersion, CreatedAtUtc, UpdatedAtUtc)
                VALUES (@DeviceId, @Serial, 'offline', @ManifestVersion, @Now, @Now);
                """,
                new { DeviceId = deviceId, manifest.Serial, manifest.ManifestVersion, Now = now },
                scope.Transaction,
                cancellationToken: cancellationToken));
        }
        else
        {
            if (manifest.ManifestVersion < existing.ManifestVersion)
            {
                throw new InvalidOperationException("Manifest version cannot move backwards.");
            }

            await scope.Connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.Devices
                SET ManifestVersion = @ManifestVersion, UpdatedAtUtc = @Now
                WHERE Id = @DeviceId;
                """,
                new { DeviceId = deviceId, manifest.ManifestVersion, Now = now },
                scope.Transaction,
                cancellationToken: cancellationToken));
        }

        await scope.Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.Capabilities SET IsActive = 0, UpdatedAtUtc = @Now WHERE DeviceId = @DeviceId;",
            new { DeviceId = deviceId, Now = now },
            scope.Transaction,
            cancellationToken: cancellationToken));

        foreach (var definition in definitions)
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
                    definition.Key,
                    definition.DisplayName,
                    Kind = definition.Kind.ToString().ToLowerInvariant(),
                    DataType = definition.DataType.ToString().ToLowerInvariant(),
                    definition.Unit,
                    definition.MinValue,
                    definition.MaxValue,
                    definition.IsWritable,
                    SupportedCommandsJson = JsonSerializer.Serialize(definition.SupportedCommands),
                    Now = now
                },
                scope.Transaction,
                cancellationToken: cancellationToken));
        }

        var pairingCodeExpiresAtUtc = await SavePairingCodeIfNeededAsync(scope, deviceId, manifest.PairingCode, now, cancellationToken);
        await scope.CommitAsync(cancellationToken);
        return new DeviceRegistrationResult(deviceId, null, pairingCodeExpiresAtUtc);
    }

    public async Task UpdatePresenceAsync(DevicePresenceMessage presence, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(presence.SchemaVersion, DeviceMessageSerializer.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported schema version.", nameof(presence));
        }

        if (presence.Status is not ("online" or "offline"))
        {
            throw new ArgumentException("Presence status must be online or offline.", nameof(presence));
        }

        await using var scope = await transactionScopeFactory.BeginAsync(cancellationToken);
        var device = await scope.Connection.QuerySingleOrDefaultAsync<PresenceDeviceRow>(new CommandDefinition(
            "SELECT Id, OwnerUserId FROM dbo.Devices WHERE Serial = @Serial;",
            new { presence.Serial },
            scope.Transaction,
            cancellationToken: cancellationToken));

        await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.Devices
            SET Status = @Status, LastSeenAtUtc = @TimestampUtc, UpdatedAtUtc = SYSUTCDATETIME()
            WHERE Serial = @Serial;
            """,
            new { presence.Status, presence.TimestampUtc, presence.Serial },
            scope.Transaction,
            cancellationToken: cancellationToken));

        if (presence.Status == "offline" && device?.OwnerUserId is not null)
        {
            await scope.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT dbo.Notifications (OwnerUserId, DeviceId, Type, Severity, Title, Message)
                SELECT @OwnerUserId, @DeviceId, 'device_offline', 'warning', 'Device offline', @Message
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM dbo.Notifications WITH (UPDLOCK, HOLDLOCK)
                    WHERE OwnerUserId = @OwnerUserId
                      AND DeviceId = @DeviceId
                      AND Type = 'device_offline'
                      AND IsRead = 0
                      AND CreatedAtUtc >= DATEADD(minute, -5, SYSUTCDATETIME())
                );
                """,
                new
                {
                    device.OwnerUserId,
                    DeviceId = device.Id,
                    Message = $"Device {presence.Serial} went offline."
                },
                scope.Transaction,
                cancellationToken: cancellationToken));
        }

        await scope.CommitAsync(cancellationToken);
    }

    private static void ValidateManifest(DeviceManifestMessage manifest)
    {
        if (!string.Equals(manifest.SchemaVersion, DeviceMessageSerializer.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported schema version.", nameof(manifest));
        }

        if (!Domain.Devices.DeviceSerial.IsValid(manifest.Serial))
        {
            throw new ArgumentException("Invalid device serial.", nameof(manifest));
        }

        if (manifest.ManifestVersion < 1 || manifest.Capabilities.Count == 0)
        {
            throw new ArgumentException("Manifest must have a positive version and at least one capability.", nameof(manifest));
        }


        if (manifest.PairingCode.Length != 8 || manifest.PairingCode.Any(character => !"ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Contains(character)))
        {
            throw new ArgumentException("Pairing code format is invalid.", nameof(manifest));
        }
    }

    private static async Task<DateTimeOffset?> SavePairingCodeIfNeededAsync(
        ISqlTransactionScope scope,
        Guid deviceId,
        string pairingCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await scope.Connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT OwnerUserId FROM dbo.Devices WHERE Id = @DeviceId;",
            new { DeviceId = deviceId },
            scope.Transaction,
            cancellationToken: cancellationToken));

        if (ownerUserId is not null)
        {
            return null;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pairingCode)));
        var existing = await scope.Connection.QuerySingleOrDefaultAsync<PairingCodeRow>(new CommandDefinition(
            "SELECT DeviceId, ExpiresAtUtc FROM dbo.PairingCodes WITH (UPDLOCK, HOLDLOCK) WHERE CodeHash = @CodeHash;",
            new { CodeHash = hash },
            scope.Transaction,
            cancellationToken: cancellationToken));

        if (existing is not null && existing.DeviceId != deviceId)
        {
            throw new InvalidOperationException("Pairing code hash already belongs to another device.");
        }

        var existingExpiresAtUtc = existing is null
            ? (DateTimeOffset?)null
            : new DateTimeOffset(DateTime.SpecifyKind(existing.ExpiresAtUtc, DateTimeKind.Utc));

        if (existingExpiresAtUtc is not null && existingExpiresAtUtc > now)
        {
            return existingExpiresAtUtc;
        }

        var expiresAtUtc = now.AddMinutes(10);
        if (existing is not null)
        {
            await scope.Connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.PairingCodes
                SET ExpiresAtUtc = @ExpiresAtUtc,
                    UsedAtUtc = NULL,
                    CreatedAtUtc = @Now
                WHERE DeviceId = @DeviceId AND CodeHash = @CodeHash;
                """,
                new { DeviceId = deviceId, CodeHash = hash, ExpiresAtUtc = expiresAtUtc, Now = now },
                scope.Transaction,
                cancellationToken: cancellationToken));
            return expiresAtUtc;
        }

        await scope.Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.PairingCodes SET ExpiresAtUtc = @Now WHERE DeviceId = @DeviceId AND UsedAtUtc IS NULL AND ExpiresAtUtc > @Now;",
            new { DeviceId = deviceId, Now = now },
            scope.Transaction,
            cancellationToken: cancellationToken));
        await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.PairingCodes (Id, DeviceId, CodeHash, ExpiresAtUtc, CreatedAtUtc)
            VALUES (@Id, @DeviceId, @CodeHash, @ExpiresAtUtc, @Now);
            """,
            new { Id = Guid.NewGuid(), DeviceId = deviceId, CodeHash = hash, ExpiresAtUtc = expiresAtUtc, Now = now },
            scope.Transaction,
            cancellationToken: cancellationToken));
        return expiresAtUtc;
    }

    private sealed record DeviceRow(Guid Id, int ManifestVersion);

    private sealed record PairingCodeRow(Guid DeviceId, DateTime ExpiresAtUtc);

    private sealed record PresenceDeviceRow(Guid Id, Guid? OwnerUserId);
}
