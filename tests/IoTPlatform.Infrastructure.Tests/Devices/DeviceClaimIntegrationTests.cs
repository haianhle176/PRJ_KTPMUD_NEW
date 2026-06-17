using System.Security.Cryptography;
using System.Text;
using Dapper;
using IoTPlatform.Application.Auth;
using IoTPlatform.Application.Devices;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Audit;
using IoTPlatform.Infrastructure.Auth;
using IoTPlatform.Infrastructure.Configuration;
using IoTPlatform.Infrastructure.Devices;
using IoTPlatform.Infrastructure.Persistence;
using IoTPlatform.Infrastructure.Persistence.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Infrastructure.Tests.Devices;

public sealed class DeviceClaimIntegrationTests
{
    private const string MasterConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;Encrypt=false;TrustServerCertificate=true";

    [Fact]
    public async Task RegisterManifestAsync_IsIdempotentForRepeatedRetainedManifestPairingCode()
    {
        var databaseName = $"IoTPlatform_RetainedManifest_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            const string serial = "DEV-P2RETAIN001";
            const string pairingCode = "RTND2345";

            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(serial, pairingCode));
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(serial, pairingCode));

            await using var connection = new SqlConnection(connectionString);
            var codeCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.PairingCodes WHERE CodeHash = @CodeHash;",
                new { CodeHash = Hash(pairingCode) });
            Assert.Equal(1, codeCount);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    [Fact]
    public async Task ClaimAsync_ClaimsDeviceAndRejectsReusedPairingCode()
    {
        var databaseName = $"IoTPlatform_Claim_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var owner = await context.AuthService.RegisterAsync(new RegisterRequestDto("owner@example.test", "Password123!", "Owner"));
            var other = await context.AuthService.RegisterAsync(new RegisterRequestDto("other@example.test", "Password123!", "Other"));
            const string serial = "DEV-P2CLAIM0001";
            const string pairingCode = "ABCD2345";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(serial, pairingCode));

            var claimed = await context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto(
                pairingCode,
                "Garden Pump",
                "garden"));

            Assert.Equal(serial, claimed.Serial);
            Assert.Equal("Garden Pump", claimed.DisplayName);
            Assert.Equal("garden", claimed.Category);
            Assert.NotNull(claimed.ClaimedAtUtc);

            await Assert.ThrowsAsync<InvalidPairingCodeException>(() => context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto(
                pairingCode,
                "Again",
                null)));
            await Assert.ThrowsAsync<InvalidPairingCodeException>(() => context.ClaimService.ClaimAsync(other.User.Id, new ClaimDeviceRequestDto(
                pairingCode,
                "Steal",
                null)));

            await using var connection = new SqlConnection(connectionString);
            var row = await connection.QuerySingleAsync<(Guid OwnerUserId, DateTime? UsedAtUtc)>(
                """
                SELECT d.OwnerUserId, pc.UsedAtUtc
                FROM dbo.Devices d
                INNER JOIN dbo.PairingCodes pc ON pc.DeviceId = d.Id
                WHERE d.Serial = @Serial AND pc.CodeHash = @CodeHash;
                """,
                new { Serial = serial, CodeHash = Hash(pairingCode) });
            Assert.Equal(owner.User.Id, row.OwnerUserId);
            Assert.NotNull(row.UsedAtUtc);

            var auditCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.AuditLogs WHERE UserId = @UserId AND Action = 'device.claim' AND EntityId = @DeviceId;",
                new { UserId = owner.User.Id, DeviceId = claimed.Id.ToString() });
            Assert.Equal(1, auditCount);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    [Fact]
    public async Task ClaimAsync_RejectsExpiredCodeAndAllowsOnlyOneConcurrentClaim()
    {
        var databaseName = $"IoTPlatform_ClaimRules_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var owner = await context.AuthService.RegisterAsync(new RegisterRequestDto("owner2@example.test", "Password123!", "Owner"));
            var other = await context.AuthService.RegisterAsync(new RegisterRequestDto("other2@example.test", "Password123!", "Other"));

            const string expiredCode = "EXPR2345";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest("DEV-P2EXPIRED001", expiredCode));
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.ExecuteAsync(
                    "UPDATE dbo.PairingCodes SET ExpiresAtUtc = DATEADD(minute, -1, SYSUTCDATETIME()) WHERE CodeHash = @CodeHash;",
                    new { CodeHash = Hash(expiredCode) });
            }

            await Assert.ThrowsAsync<InvalidPairingCodeException>(() => context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto(
                expiredCode,
                "Expired",
                null)));

            const string concurrentCode = "CNCP2345";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest("DEV-P2CONCUR001", concurrentCode));
            var attempts = new[]
            {
                TryClaimAsync(context.ClaimService, owner.User.Id, concurrentCode, "Owner Device"),
                TryClaimAsync(context.ClaimService, other.User.Id, concurrentCode, "Other Device")
            };

            var results = await Task.WhenAll(attempts);
            Assert.Single(results.Where(result => result is not null));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    [Fact]
    public async Task DeviceManagement_EnforcesOwnershipForListDetailUpdateAndUnclaim()
    {
        var databaseName = $"IoTPlatform_DeviceAccess_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var owner = await context.AuthService.RegisterAsync(new RegisterRequestDto("owner3@example.test", "Password123!", "Owner"));
            var other = await context.AuthService.RegisterAsync(new RegisterRequestDto("other3@example.test", "Password123!", "Other"));
            const string pairingCode = "MGMT2345";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest("DEV-P2MGMT0001", pairingCode));

            var claimed = await context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto(pairingCode, "Room Sensor", "room"));

            var ownerDevices = await context.ManagementService.ListAsync(owner.User.Id);
            var otherDevices = await context.ManagementService.ListAsync(other.User.Id);
            Assert.Single(ownerDevices);
            Assert.Empty(otherDevices);
            Assert.Equal(claimed.Id, (await context.ManagementService.GetAsync(owner.User.Id, claimed.Id)).Id);
            await Assert.ThrowsAsync<DeviceNotFoundException>(() => context.ManagementService.GetAsync(other.User.Id, claimed.Id));

            var updated = await context.ManagementService.UpdateAsync(owner.User.Id, claimed.Id, new UpdateDeviceRequestDto("Living Room Sensor", "living-room"));
            Assert.Equal("Living Room Sensor", updated.DisplayName);
            Assert.Equal("living-room", updated.Category);
            await Assert.ThrowsAsync<DeviceNotFoundException>(() => context.ManagementService.UpdateAsync(other.User.Id, claimed.Id, new UpdateDeviceRequestDto("Steal", null)));
            await Assert.ThrowsAsync<DeviceNotFoundException>(() => context.ManagementService.UnclaimAsync(other.User.Id, claimed.Id));

            await context.ManagementService.UnclaimAsync(owner.User.Id, claimed.Id);
            Assert.Empty(await context.ManagementService.ListAsync(owner.User.Id));

            await using var connection = new SqlConnection(connectionString);
            var auditActions = await connection.QueryAsync<string>(
                "SELECT Action FROM dbo.AuditLogs WHERE UserId = @UserId ORDER BY Id;",
                new { UserId = owner.User.Id });
            Assert.Contains("device.claim", auditActions);
            Assert.Contains("device.update", auditActions);
            Assert.Contains("device.unclaim", auditActions);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    [Fact]
    public async Task ListAvailableAsync_ReturnsOnlyUnclaimedDevicesWithActivePairingCode()
    {
        var databaseName = $"IoTPlatform_AvailableDevices_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var user = await context.AuthService.RegisterAsync(new RegisterRequestDto("available@example.test", "Password123!", "Available User"));

            const string onlineSerial = "DEV-P9AVAIL001";
            const string offlineSerial = "DEV-P9AVAIL002";
            const string claimedSerial = "DEV-P9CLAIM001";
            const string expiredSerial = "DEV-P9EXPIRE01";
            const string usedSerial = "DEV-P9USED0001";

            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(onlineSerial, "AVAL2345", [Sensor("temperature"), Actuator("power")]));
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(offlineSerial, "AVAL3456", [Sensor("humidity")]));
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(claimedSerial, "AVAL4567", [Actuator("relay")]));
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(expiredSerial, "AVAL5678", [Sensor("light")]));
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(usedSerial, "AVAL6789", [Actuator("lock")]));
            await context.DeviceRegistry.UpdatePresenceAsync(CreatePresence(onlineSerial, "online"));

            await context.ClaimService.ClaimAsync(user.User.Id, new ClaimDeviceRequestDto("AVAL4567", "Claimed", null));

            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE dbo.Devices
                    SET Status = 'offline', LastSeenAtUtc = DATEADD(minute, 5, SYSUTCDATETIME())
                    WHERE Serial = @Serial;

                    UPDATE dbo.PairingCodes
                    SET ExpiresAtUtc = DATEADD(minute, -1, SYSUTCDATETIME())
                    WHERE CodeHash = @ExpiredHash;

                    UPDATE dbo.PairingCodes
                    SET UsedAtUtc = SYSUTCDATETIME()
                    WHERE CodeHash = @UsedHash;
                    """,
                    new
                    {
                        Serial = offlineSerial,
                        ExpiredHash = Hash("AVAL5678"),
                        UsedHash = Hash("AVAL6789")
                    });
            }

            var available = await context.ManagementService.ListAvailableAsync(user.User.Id);

            Assert.Equal([onlineSerial], available.Select(device => device.Serial).ToArray());
            var online = Assert.Single(available.Where(device => device.Serial == onlineSerial));
            Assert.Equal("online", online.Status);
            Assert.Equal("custom", online.DeviceType);
            Assert.Equal(1, online.SensorCount);
            Assert.Equal(1, online.ActuatorCount);
            Assert.True(online.PairingCodeExpiresAtUtc > DateTimeOffset.UtcNow);

            Assert.DoesNotContain(available, device => device.Serial == offlineSerial);
            Assert.DoesNotContain(available, device => device.Serial == claimedSerial);
            Assert.DoesNotContain(available, device => device.Serial == expiredSerial);
            Assert.DoesNotContain(available, device => device.Serial == usedSerial);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    private static DeviceManifestMessage CreateManifest(string serial, string pairingCode) => new(
        Guid.NewGuid(),
        serial,
        DateTimeOffset.UtcNow,
        DeviceMessageSerializer.CurrentSchemaVersion,
        1,
        pairingCode,
        [new CapabilityManifestItem("pump_power", "Pump Power", "actuator", "boolean", null, null, null, true, ["ON", "OFF"])]);

    private static DeviceManifestMessage CreateManifest(string serial, string pairingCode, IReadOnlyList<CapabilityManifestItem> capabilities) => new(
        Guid.NewGuid(),
        serial,
        DateTimeOffset.UtcNow,
        DeviceMessageSerializer.CurrentSchemaVersion,
        1,
        pairingCode,
        capabilities);

    private static DevicePresenceMessage CreatePresence(string serial, string status) => new(
        Guid.NewGuid(),
        serial,
        DateTimeOffset.UtcNow,
        DeviceMessageSerializer.CurrentSchemaVersion,
        status,
        Guid.NewGuid());

    private static CapabilityManifestItem Sensor(string key) =>
        new(key, key, "sensor", "number", "C", -20, 80, false, []);

    private static CapabilityManifestItem Actuator(string key) =>
        new(key, key, "actuator", "boolean", null, null, null, true, ["ON", "OFF"]);

    private static async Task<TestContext> CreateContextAsync(string connectionString)
    {
        var databaseOptions = Options.Create(new DatabaseOptions { IoTPlatform = connectionString });
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "IoTPlatform.Tests",
            Audience = "IoTPlatform.Tests",
            SigningKey = "test-signing-key-that-is-long-enough-123456",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 30
        });
        var connectionFactory = new SqlConnectionFactory(databaseOptions);
        await new SqlMigrationRunner(connectionFactory, new EmbeddedSqlMigrationSource()).MigrateAsync();
        var transactionFactory = new SqlTransactionScopeFactory(connectionFactory);
        var authRepository = new SqlAuthRepository(transactionFactory, connectionFactory);
        var auditRepository = new SqlAuditLogRepository(connectionFactory);
        var authService = new AuthService(authRepository, new PasswordService(), new JwtTokenService(jwtOptions), auditRepository);
        var deviceRegistry = new DeviceRegistry(transactionFactory);
        var claimService = new DeviceClaimService(new SqlDeviceClaimRepository(transactionFactory), auditRepository);
        var managementService = new DeviceManagementService(new SqlDeviceManagementRepository(connectionFactory), auditRepository);
        return new TestContext(authService, deviceRegistry, claimService, managementService);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task<DeviceSummaryDto?> TryClaimAsync(IDeviceClaimService claimService, Guid userId, string pairingCode, string displayName)
    {
        try
        {
            return await claimService.ClaimAsync(userId, new ClaimDeviceRequestDto(pairingCode, displayName, null));
        }
        catch (Exception exception) when (exception is InvalidPairingCodeException or DeviceAlreadyClaimedException)
        {
            return null;
        }
    }

    private static async Task ExecuteMasterAsync(string sql)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql);
    }

    private sealed record TestContext(
        IAuthService AuthService,
        DeviceRegistry DeviceRegistry,
        IDeviceClaimService ClaimService,
        IDeviceManagementService ManagementService);
}
