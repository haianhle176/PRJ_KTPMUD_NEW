using System.Text.Json;
using Dapper;
using IoTPlatform.Application.Auth;
using IoTPlatform.Application.Automation;
using IoTPlatform.Application.Devices;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Auth;
using IoTPlatform.Infrastructure.Automation;
using IoTPlatform.Infrastructure.Configuration;
using IoTPlatform.Infrastructure.Devices;
using IoTPlatform.Infrastructure.Persistence;
using IoTPlatform.Infrastructure.Persistence.Migrations;
using IoTPlatform.Infrastructure.Telemetry;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Infrastructure.Tests.Automation;

public sealed class AutomationPipelineIntegrationTests
{
    private const string MasterConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;Encrypt=false;TrustServerCertificate=true";

    [Fact]
    public async Task AutomationPipeline_TriggersCommandOnEdgeAndSkipsDuplicateCooldownAndDisabledRule()
    {
        var databaseName = $"IoTPlatform_Automation_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var owner = await context.AuthService.RegisterAsync(new RegisterRequestDto("automation@example.test", "Password123!", "Automation Owner"));
            const string serial = "DEV-P5AUTO0001";
            const string pairingCode = "AUTA2345";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(serial, pairingCode));
            var device = await context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto(pairingCode, "Room Controller", "room"));

            var rule = await context.AutomationService.CreateAsync(owner.User.Id, new CreateAutomationRuleRequestDto(
                "Hot room turns pump on",
                device.Id,
                "temperature",
                ">",
                JsonSerializer.SerializeToElement(30m, DeviceMessageSerializer.Options),
                device.Id,
                "pump_power",
                "ON",
                JsonSerializer.SerializeToElement(true, DeviceMessageSerializer.Options),
                CooldownSeconds: 60,
                IsEnabled: true));
            Assert.True(rule.IsEnabled);

            await context.TelemetryRepository.IngestAsync(CreateTelemetry(serial, Guid.NewGuid(), 25m), MqttTopics.Telemetry(serial));
            Assert.Equal(0, await CountCommandsAsync(connectionString));
            Assert.Equal("skipped", await LastExecutionOutcomeAsync(connectionString, rule.Id));

            var triggerMessageId = Guid.NewGuid();
            var trigger = CreateTelemetry(serial, triggerMessageId, 35m);
            await context.TelemetryRepository.IngestAsync(trigger, MqttTopics.Telemetry(serial));
            await context.TelemetryRepository.IngestAsync(trigger, MqttTopics.Telemetry(serial));
            Assert.Equal(1, await CountCommandsAsync(connectionString));
            var generatedCommand = await GetLatestAutomationCommandAsync(connectionString);
            Assert.Equal(device.Id, generatedCommand.DeviceId);
            Assert.Equal("pump_power", generatedCommand.CapabilityKey);
            Assert.Equal("ON", generatedCommand.CommandName);
            Assert.True(generatedCommand.DesiredBooleanValue);
            Assert.Equal("pending", generatedCommand.Status);
            Assert.Equal("triggered", await LastExecutionOutcomeAsync(connectionString, rule.Id));

            await context.TelemetryRepository.IngestAsync(CreateTelemetry(serial, Guid.NewGuid(), 36m), MqttTopics.Telemetry(serial));
            Assert.Equal(1, await CountCommandsAsync(connectionString));

            var executions = await context.AutomationService.ListExecutionsAsync(owner.User.Id, rule.Id, 20);
            Assert.Contains(executions, execution => execution.Outcome == "triggered");
            Assert.Contains(executions, execution => execution.Outcome == "skipped" && execution.Reason == "condition_already_true");

            await context.AutomationService.UpdateAsync(owner.User.Id, rule.Id, new UpdateAutomationRuleRequestDto(
                rule.Name,
                rule.SourceDeviceId,
                rule.SourceCapabilityKey,
                rule.ComparisonOperator,
                JsonSerializer.SerializeToElement(30m, DeviceMessageSerializer.Options),
                rule.TargetDeviceId,
                rule.TargetCapabilityKey,
                rule.CommandName,
                JsonSerializer.SerializeToElement(true, DeviceMessageSerializer.Options),
                rule.CooldownSeconds,
                IsEnabled: false));
            await context.TelemetryRepository.IngestAsync(CreateTelemetry(serial, Guid.NewGuid(), 20m), MqttTopics.Telemetry(serial));
            await context.TelemetryRepository.IngestAsync(CreateTelemetry(serial, Guid.NewGuid(), 40m), MqttTopics.Telemetry(serial));
            Assert.Equal(1, await CountCommandsAsync(connectionString));

            await context.AutomationService.DeleteAsync(owner.User.Id, rule.Id);
            Assert.Empty(await context.AutomationService.ListAsync(owner.User.Id));
            await using var connection = new SqlConnection(connectionString);
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AutomationExecutions WHERE AutomationRuleId = @RuleId;", new { RuleId = rule.Id }));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DeviceCommands WHERE Id = @CommandId AND AutomationRuleId IS NULL;", new { CommandId = generatedCommand.Id }));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    [Fact]
    public async Task UnclaimDevice_RemovesRelatedRulesAndExecutionsButPreservesCommandHistory()
    {
        var databaseName = $"IoTPlatform_AutomationUnclaim_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var owner = await context.AuthService.RegisterAsync(new RegisterRequestDto("automation-unclaim@example.test", "Password123!", "Owner"));
            const string serial = "DEV-P5REMOVE01";
            const string pairingCode = "RMVE2345";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(serial, pairingCode));
            var device = await context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto(pairingCode, "Removable Device", null));
            var rule = await context.AutomationService.CreateAsync(owner.User.Id, ValidRequest(device.Id));

            await context.TelemetryRepository.IngestAsync(CreateTelemetry(serial, Guid.NewGuid(), 20m), MqttTopics.Telemetry(serial));
            await context.TelemetryRepository.IngestAsync(CreateTelemetry(serial, Guid.NewGuid(), 40m), MqttTopics.Telemetry(serial));
            Assert.Equal("triggered", await LastExecutionOutcomeAsync(connectionString, rule.Id));

            await context.ManagementService.UnclaimAsync(owner.User.Id, device.Id);

            Assert.Empty(await context.AutomationService.ListAsync(owner.User.Id));
            await using var connection = new SqlConnection(connectionString);
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AutomationRules;"));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AutomationExecutions;"));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DeviceCommands;"));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DeviceCommands WHERE AutomationRuleId IS NOT NULL;"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    [Fact]
    public async Task AutomationService_RejectsInvalidOwnershipAndCapabilityTypes()
    {
        var databaseName = $"IoTPlatform_AutomationReject_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var owner = await context.AuthService.RegisterAsync(new RegisterRequestDto("automation-owner@example.test", "Password123!", "Owner"));
            var other = await context.AuthService.RegisterAsync(new RegisterRequestDto("automation-other@example.test", "Password123!", "Other"));
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest("DEV-P5OWNER001", "AWNR2345"));
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest("DEV-P5OTHER001", "ATHR2345"));
            var ownerDevice = await context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto("AWNR2345", "Owner Device", null));
            var otherDevice = await context.ClaimService.ClaimAsync(other.User.Id, new ClaimDeviceRequestDto("ATHR2345", "Other Device", null));

            await Assert.ThrowsAsync<AutomationValidationException>(() => context.AutomationService.CreateAsync(owner.User.Id, ValidRequest(ownerDevice.Id) with
            {
                TargetDeviceId = otherDevice.Id
            }));
            await Assert.ThrowsAsync<AutomationValidationException>(() => context.AutomationService.CreateAsync(owner.User.Id, ValidRequest(ownerDevice.Id) with
            {
                SourceCapabilityKey = "pump_power"
            }));
            await Assert.ThrowsAsync<AutomationValidationException>(() => context.AutomationService.CreateAsync(owner.User.Id, ValidRequest(ownerDevice.Id) with
            {
                ThresholdValue = JsonSerializer.SerializeToElement(true, DeviceMessageSerializer.Options)
            }));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    [Fact]
    public async Task AutomationPipeline_RespectsScheduleAndCreatesThresholdNotification()
    {
        var databaseName = $"IoTPlatform_AutomationSchedule_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var context = await CreateContextAsync(connectionString);
            var owner = await context.AuthService.RegisterAsync(new RegisterRequestDto("automation-schedule@example.test", "Password123!", "Automation Schedule"));
            const string serial = "DEV-P8SCHED001";
            const string pairingCode = "SCHD2345";
            await context.DeviceRegistry.RegisterManifestAsync(CreateManifest(serial, pairingCode));
            var device = await context.ClaimService.ClaimAsync(owner.User.Id, new ClaimDeviceRequestDto(pairingCode, "Scheduled Device", "room"));
            var inactiveTodayMask = 127 ^ (1 << (int)DateTimeOffset.UtcNow.DayOfWeek);

            var outsideRule = await context.AutomationService.CreateAsync(owner.User.Id, ValidRequest(device.Id) with
            {
                Name = "Outside window",
                ActiveDaysOfWeek = inactiveTodayMask
            });

            await context.TelemetryRepository.IngestAsync(CreateTelemetry(serial, Guid.NewGuid(), 40m), MqttTopics.Telemetry(serial));

            Assert.Equal(0, await CountCommandsAsync(connectionString));
            Assert.Equal("outside_schedule", await LastExecutionReasonAsync(connectionString, outsideRule.Id));

            var insideRule = await context.AutomationService.CreateAsync(owner.User.Id, ValidRequest(device.Id) with
            {
                Name = "Always window",
                ActiveFromTime = "00:00",
                ActiveToTime = "23:59"
            });

            await context.TelemetryRepository.IngestAsync(CreateTelemetry(serial, Guid.NewGuid(), 20m), MqttTopics.Telemetry(serial));
            await context.TelemetryRepository.IngestAsync(CreateTelemetry(serial, Guid.NewGuid(), 40m), MqttTopics.Telemetry(serial));

            Assert.Equal(1, await CountCommandsAsync(connectionString));
            Assert.Equal(1, await CountNotificationsAsync(connectionString, owner.User.Id, "threshold_triggered"));
            Assert.Equal("triggered", await LastExecutionOutcomeAsync(connectionString, insideRule.Id));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    private static CreateAutomationRuleRequestDto ValidRequest(Guid deviceId) => new(
        "Hot room",
        deviceId,
        "temperature",
        ">",
        JsonSerializer.SerializeToElement(30m, DeviceMessageSerializer.Options),
        deviceId,
        "pump_power",
        "ON",
        JsonSerializer.SerializeToElement(true, DeviceMessageSerializer.Options),
        30,
        true);

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

    private static DeviceTelemetryMessage CreateTelemetry(string serial, Guid messageId, decimal temperature) => new(
        messageId,
        serial,
        DateTimeOffset.UtcNow,
        DeviceMessageSerializer.CurrentSchemaVersion,
        [new TelemetryReadingItem("temperature", JsonSerializer.SerializeToElement(temperature, DeviceMessageSerializer.Options))]);

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
        return new TestContext(
            new AuthService(new SqlAuthRepository(transactionFactory, connectionFactory), new PasswordService(), new JwtTokenService(jwtOptions)),
            new DeviceRegistry(transactionFactory),
            new DeviceClaimService(new SqlDeviceClaimRepository(transactionFactory)),
            new DeviceManagementService(new SqlDeviceManagementRepository(connectionFactory, transactionFactory)),
            new SqlTelemetryIngestionRepository(transactionFactory),
            new AutomationRuleService(new SqlAutomationRuleRepository(connectionFactory)));
    }

    private static async Task<int> CountCommandsAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.DeviceCommands WHERE Source = 'automation';");
    }

    private static async Task<AutomationCommandRow> GetLatestAutomationCommandAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleAsync<AutomationCommandRow>(
            """
            SELECT TOP (1)
                dc.Id,
                dc.DeviceId,
                capability.[Key] AS CapabilityKey,
                dc.CommandName,
                dc.DesiredBooleanValue,
                dc.Status
            FROM dbo.DeviceCommands dc
            INNER JOIN dbo.Capabilities capability ON capability.Id = dc.CapabilityId
            WHERE dc.Source = 'automation'
            ORDER BY dc.CreatedAtUtc DESC;
            """);
    }

    private static async Task<string> LastExecutionOutcomeAsync(string connectionString, Guid ruleId)
    {
        await using var connection = new SqlConnection(connectionString);
        var outcome = await connection.ExecuteScalarAsync<string>(
            "SELECT TOP (1) Outcome FROM dbo.AutomationExecutions WHERE AutomationRuleId = @RuleId ORDER BY Id DESC;",
            new { RuleId = ruleId });
        Assert.False(string.IsNullOrWhiteSpace(outcome));
        return outcome;
    }

    private static async Task<string> LastExecutionReasonAsync(string connectionString, Guid ruleId)
    {
        await using var connection = new SqlConnection(connectionString);
        var reason = await connection.ExecuteScalarAsync<string>(
            "SELECT TOP (1) Reason FROM dbo.AutomationExecutions WHERE AutomationRuleId = @RuleId ORDER BY Id DESC;",
            new { RuleId = ruleId });
        Assert.False(string.IsNullOrWhiteSpace(reason));
        return reason;
    }

    private static async Task<int> CountNotificationsAsync(string connectionString, Guid userId, string type)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Notifications WHERE OwnerUserId = @UserId AND Type = @Type;",
            new { UserId = userId, Type = type });
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
        IDeviceManagementService ManagementService,
        SqlTelemetryIngestionRepository TelemetryRepository,
        IAutomationRuleService AutomationService);

    private sealed record AutomationCommandRow(
        Guid Id,
        Guid DeviceId,
        string CapabilityKey,
        string CommandName,
        bool DesiredBooleanValue,
        string Status);
}
