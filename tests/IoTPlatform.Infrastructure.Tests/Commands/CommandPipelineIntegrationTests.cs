using System.Text.Json;
using Dapper;
using IoTPlatform.Application.Auth;
using IoTPlatform.Application.Commands;
using IoTPlatform.Application.Devices;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Auth;
using IoTPlatform.Infrastructure.Commands;
using IoTPlatform.Infrastructure.Configuration;
using IoTPlatform.Infrastructure.Devices;
using IoTPlatform.Infrastructure.Persistence;
using IoTPlatform.Infrastructure.Persistence.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Infrastructure.Tests.Commands;

public sealed class CommandPipelineIntegrationTests
{
    private const string MasterConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;Encrypt=false;TrustServerCertificate=true";

    [Fact]
    public async Task CommandPipeline_CreatesDispatchesAndAppliesCompletedAckIdempotently()
    {
        var databaseName = $"IoTPlatform_Command_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var owner = await context.AuthService.RegisterAsync(new RegisterRequestDto("command@example.test", "Password123!", "Command Owner"));
            const string serial = "DEV-P4CMD000001";
            const string pairingCode = "CMND2345";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(serial, pairingCode));
            var device = await context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto(pairingCode, "Pump", "garden"));

            var command = await context.CommandService.CreateManualAsync(owner.User.Id, new CreateCommandRequestDto(
                device.Id,
                "pump_power",
                "ON",
                JsonSerializer.SerializeToElement(true, DeviceMessageSerializer.Options)));
            Assert.Equal("pending", command.Status);

            var pending = await context.DispatchRepository.GetPendingAsync(10);
            var pendingCommand = Assert.Single(pending);
            Assert.Equal(command.Id, pendingCommand.Id);
            Assert.Equal(serial, pendingCommand.Serial);
            Assert.True(await context.DispatchRepository.MarkSentAsync(command.Id));
            Assert.Equal("sent", (await context.CommandService.GetAsync(owner.User.Id, command.Id)).Status);

            var acknowledgement = new DeviceCommandAcknowledgementMessage(
                Guid.NewGuid(),
                serial,
                DateTimeOffset.UtcNow,
                DeviceMessageSerializer.CurrentSchemaVersion,
                command.Id,
                "completed",
                JsonSerializer.SerializeToElement(true, DeviceMessageSerializer.Options),
                null,
                null);
            var ackResult = await context.DispatchRepository.ApplyAcknowledgementAsync(acknowledgement);
            var duplicateAck = await context.DispatchRepository.ApplyAcknowledgementAsync(acknowledgement);

            Assert.False(ackResult.WasDuplicate);
            Assert.True(duplicateAck.WasDuplicate);
            var completed = await context.CommandService.GetAsync(owner.User.Id, command.Id);
            Assert.Equal("completed", completed.Status);
            Assert.NotNull(completed.CompletedAtUtc);

            await using var connection = new SqlConnection(connectionString);
            var ackCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.CommandAcknowledgements WHERE CommandId = @CommandId;", new { CommandId = command.Id });
            Assert.Equal(1, ackCount);
            var latestActuatorValue = await connection.QuerySingleAsync<bool>(
                """
                SELECT lv.BooleanValue
                FROM dbo.DeviceLatestValues lv
                INNER JOIN dbo.Capabilities capability ON capability.Id = lv.CapabilityId
                WHERE lv.DeviceId = @DeviceId AND capability.[Key] = 'pump_power';
                """,
                new { DeviceId = device.Id });
            Assert.True(latestActuatorValue);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    [Fact]
    public async Task CommandService_RejectsCrossUserSensorWrongTypeAndUnsupportedCommand()
    {
        var databaseName = $"IoTPlatform_CommandReject_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var owner = await context.AuthService.RegisterAsync(new RegisterRequestDto("command-owner@example.test", "Password123!", "Owner"));
            var other = await context.AuthService.RegisterAsync(new RegisterRequestDto("command-other@example.test", "Password123!", "Other"));
            const string pairingCode = "RJCM2345";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest("DEV-P4REJECT001", pairingCode));
            var device = await context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto(pairingCode, "Pump", "garden"));

            var booleanValue = JsonSerializer.SerializeToElement(true, DeviceMessageSerializer.Options);
            var numberValue = JsonSerializer.SerializeToElement(1m, DeviceMessageSerializer.Options);

            await Assert.ThrowsAsync<CommandValidationException>(() => context.CommandService.CreateManualAsync(other.User.Id, new CreateCommandRequestDto(device.Id, "pump_power", "ON", booleanValue)));
            await Assert.ThrowsAsync<CommandValidationException>(() => context.CommandService.CreateManualAsync(owner.User.Id, new CreateCommandRequestDto(device.Id, "temperature", "ON", numberValue)));
            await Assert.ThrowsAsync<CommandValidationException>(() => context.CommandService.CreateManualAsync(owner.User.Id, new CreateCommandRequestDto(device.Id, "pump_power", "ON", numberValue)));
            await Assert.ThrowsAsync<CommandValidationException>(() => context.CommandService.CreateManualAsync(owner.User.Id, new CreateCommandRequestDto(device.Id, "pump_power", "TOGGLE", booleanValue)));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    [Fact]
    public async Task DispatchRepository_RetriesSentCommandsAndTimesOutExpiredCommands()
    {
        var databaseName = $"IoTPlatform_CommandRetry_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var owner = await context.AuthService.RegisterAsync(new RegisterRequestDto("retry@example.test", "Password123!", "Retry Owner"));
            const string pairingCode = "RTRY2345";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest("DEV-P4RETRY001", pairingCode));
            var device = await context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto(pairingCode, "Pump", "garden"));
            var command = await context.CommandService.CreateManualAsync(owner.User.Id, new CreateCommandRequestDto(
                device.Id,
                "pump_power",
                "ON",
                JsonSerializer.SerializeToElement(true, DeviceMessageSerializer.Options)));

            Assert.True(await context.DispatchRepository.MarkSentAsync(command.Id));
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.ExecuteAsync(
                    "UPDATE dbo.DeviceCommands SET SentAtUtc = DATEADD(second, -10, SYSUTCDATETIME()) WHERE Id = @CommandId;",
                    new { CommandId = command.Id });
            }

            var retryable = await context.DispatchRepository.GetPendingAsync(10);
            Assert.Contains(retryable, item => item.Id == command.Id);

            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.ExecuteAsync(
                    "UPDATE dbo.DeviceCommands SET ExpiresAtUtc = DATEADD(second, -1, SYSUTCDATETIME()) WHERE Id = @CommandId;",
                    new { CommandId = command.Id });
            }

            Assert.Empty(await context.DispatchRepository.GetPendingAsync(10));
            Assert.Equal("timed_out", (await context.CommandService.GetAsync(owner.User.Id, command.Id)).Status);
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
        [
            new CapabilityManifestItem("temperature", "Temperature", "sensor", "number", "C", -20, 80, false, []),
            new CapabilityManifestItem("pump_power", "Pump Power", "actuator", "boolean", null, null, null, true, ["ON", "OFF"])
        ]);

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
        var authService = new AuthService(new SqlAuthRepository(transactionFactory, connectionFactory), new PasswordService(), new JwtTokenService(jwtOptions));
        var deviceRegistry = new DeviceRegistry(transactionFactory);
        var claimService = new DeviceClaimService(new SqlDeviceClaimRepository(transactionFactory));
        var commandService = new CommandService(new SqlCommandRepository(connectionFactory));
        var dispatchRepository = new SqlCommandDispatchRepository(transactionFactory);

        return new TestContext(authService, deviceRegistry, claimService, commandService, dispatchRepository);
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
        ICommandService CommandService,
        ICommandDispatchRepository DispatchRepository);
}
