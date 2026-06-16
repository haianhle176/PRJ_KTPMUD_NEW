using System.Text.Json;
using Dapper;
using IoTPlatform.Application.Auth;
using IoTPlatform.Application.Devices;
using IoTPlatform.Application.Telemetry;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Auth;
using IoTPlatform.Infrastructure.Configuration;
using IoTPlatform.Infrastructure.Devices;
using IoTPlatform.Infrastructure.Persistence;
using IoTPlatform.Infrastructure.Persistence.Migrations;
using IoTPlatform.Infrastructure.Telemetry;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Infrastructure.Tests.Telemetry;

public sealed class TelemetryPipelineIntegrationTests
{
    private const string MasterConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;Encrypt=false;TrustServerCertificate=true";

    [Fact]
    public async Task IngestAsync_StoresHistoryLatestValuesAndRejectsDuplicateMessage()
    {
        var databaseName = $"IoTPlatform_Telemetry_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var owner = await context.AuthService.RegisterAsync(new RegisterRequestDto("telemetry@example.test", "Password123!", "Telemetry Owner"));
            var other = await context.AuthService.RegisterAsync(new RegisterRequestDto("telemetry-other@example.test", "Password123!", "Other Owner"));
            const string serial = "DEV-P3TELEM0001";
            const string pairingCode = "TLMY2345";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(serial, pairingCode));
            var device = await context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto(pairingCode, "Room Sensor", "room"));

            var telemetry = CreateTelemetry(serial, Guid.NewGuid(), ("temperature", 28.75m), ("motion", true));
            var result = await context.IngestionRepository.IngestAsync(telemetry, MqttTopics.Telemetry(serial));
            var duplicate = await context.IngestionRepository.IngestAsync(telemetry, MqttTopics.Telemetry(serial));

            Assert.False(result.WasDuplicate);
            Assert.Equal(2, result.InsertedReadingCount);
            Assert.True(duplicate.WasDuplicate);
            Assert.Equal(0, duplicate.InsertedReadingCount);

            var latest = await context.QueryService.GetLatestValuesAsync(owner.User.Id, device.Id);
            Assert.Equal(2, latest.Count);
            Assert.Contains(latest, value => value.CapabilityKey == "temperature" && value.NumberValue == 28.75m);
            Assert.Contains(latest, value => value.CapabilityKey == "motion" && value.BooleanValue == true);

            var history = await context.QueryService.GetHistoryAsync(owner.User.Id, device.Id, "temperature", null, null, page: 1, pageSize: 20);
            Assert.Equal(1, history.TotalCount);
            Assert.Single(history.Items);
            Assert.Equal("temperature", history.Items[0].CapabilityKey);

            Assert.Empty(await context.QueryService.GetLatestValuesAsync(other.User.Id, device.Id));
            var otherHistory = await context.QueryService.GetHistoryAsync(other.User.Id, device.Id, null, null, null, page: 1, pageSize: 20);
            Assert.Equal(0, otherHistory.TotalCount);

            await using var connection = new SqlConnection(connectionString);
            var recordCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.TelemetryRecords;");
            var latestCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DeviceLatestValues;");
            var deviceStatus = await connection.QuerySingleAsync<(string Status, DateTime? LastSeenAtUtc)>(
                "SELECT Status, LastSeenAtUtc FROM dbo.Devices WHERE Id = @DeviceId;",
                new { DeviceId = device.Id });
            Assert.Equal(2, recordCount);
            Assert.Equal(2, latestCount);
            Assert.Equal("online", deviceStatus.Status);
            Assert.NotNull(deviceStatus.LastSeenAtUtc);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    [Fact]
    public async Task IngestAsync_RejectsMalformedUnknownWrongTypeAndActuatorReadings()
    {
        var databaseName = $"IoTPlatform_TelemetryReject_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            const string serial = "DEV-P3REJECT001";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(serial, "RJCT2345"));

            await Assert.ThrowsAsync<TelemetryIngestionException>(() => context.IngestionRepository.IngestAsync(
                CreateTelemetry(serial, Guid.NewGuid(), ("unknown", 1m)),
                MqttTopics.Telemetry(serial)));

            await Assert.ThrowsAsync<TelemetryIngestionException>(() => context.IngestionRepository.IngestAsync(
                CreateTelemetry(serial, Guid.NewGuid(), ("temperature", "hot")),
                MqttTopics.Telemetry(serial)));

            await Assert.ThrowsAsync<TelemetryIngestionException>(() => context.IngestionRepository.IngestAsync(
                CreateTelemetry(serial, Guid.NewGuid(), ("switch", true)),
                MqttTopics.Telemetry(serial)));

            await Assert.ThrowsAsync<TelemetryIngestionException>(() => context.IngestionRepository.IngestAsync(
                new DeviceTelemetryMessage(Guid.Empty, serial, DateTimeOffset.UtcNow, DeviceMessageSerializer.CurrentSchemaVersion, []),
                MqttTopics.Telemetry(serial)));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    [Fact]
    public async Task IngestAsync_HandlesOneHundredDevicesWithOneReadingEach()
    {
        var databaseName = $"IoTPlatform_TelemetryLoad_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            for (var index = 0; index < 100; index++)
            {
                var serial = $"DEV-P3LOAD{index:000000}";
                await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(serial, CreatePairingCode(index)));
                await context.IngestionRepository.IngestAsync(
                    CreateTelemetry(serial, Guid.NewGuid(), ("temperature", 20m + index / 10m)),
                    MqttTopics.Telemetry(serial));
            }

            await using var connection = new SqlConnection(connectionString);
            var recordCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.TelemetryRecords;");
            var latestCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DeviceLatestValues;");
            var processedCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.ProcessedMessages WHERE MessageType = 'telemetry';");
            Assert.Equal(100, recordCount);
            Assert.Equal(100, latestCount);
            Assert.Equal(100, processedCount);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    private static string CreatePairingCode(int index)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> code = stackalloc char[8];
        var value = index;
        for (var position = code.Length - 1; position >= 0; position--)
        {
            code[position] = alphabet[value % alphabet.Length];
            value /= alphabet.Length;
        }

        return new string(code);
    }

    private static DeviceManifestMessage CreateManifest(string serial, string pairingCode) => new(
        Guid.NewGuid(),
        serial,
        DateTimeOffset.UtcNow,
        DeviceMessageSerializer.CurrentSchemaVersion,
        1,
        pairingCode,
        [
            new CapabilityManifestItem("temperature", "Temperature", "sensor", "number", "C", -20, 80, false, []),
            new CapabilityManifestItem("motion", "Motion", "sensor", "boolean", null, null, null, false, []),
            new CapabilityManifestItem("switch", "Switch", "actuator", "boolean", null, null, null, true, ["ON", "OFF"])
        ]);

    private static DeviceTelemetryMessage CreateTelemetry(string serial, Guid messageId, params (string Key, object Value)[] readings) => new(
        messageId,
        serial,
        DateTimeOffset.UtcNow,
        DeviceMessageSerializer.CurrentSchemaVersion,
        readings.Select(reading => new TelemetryReadingItem(
            reading.Key,
            JsonSerializer.SerializeToElement(reading.Value, DeviceMessageSerializer.Options))).ToArray());

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
        var authService = new AuthService(authRepository, new PasswordService(), new JwtTokenService(jwtOptions));
        var deviceRegistry = new DeviceRegistry(transactionFactory);
        var claimService = new DeviceClaimService(new SqlDeviceClaimRepository(transactionFactory));
        var ingestionRepository = new SqlTelemetryIngestionRepository(transactionFactory);
        var queryService = new TelemetryQueryService(new SqlTelemetryQueryRepository(connectionFactory));

        return new TestContext(authService, deviceRegistry, claimService, ingestionRepository, queryService);
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
        ITelemetryIngestionRepository IngestionRepository,
        ITelemetryQueryService QueryService);
}
