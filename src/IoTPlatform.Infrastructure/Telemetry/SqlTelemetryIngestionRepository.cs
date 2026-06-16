using System.Data;
using System.Text.Json;
using Dapper;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Telemetry;

public sealed class SqlTelemetryIngestionRepository(ISqlTransactionScopeFactory transactionScopeFactory) : ITelemetryIngestionRepository
{
    public async Task<TelemetryIngestionResult> IngestAsync(
        DeviceTelemetryMessage telemetry,
        string topic,
        CancellationToken cancellationToken = default)
    {
        ValidateEnvelope(telemetry);

        await using var scope = await transactionScopeFactory.BeginAsync(cancellationToken);
        var processed = await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.ProcessedMessages (MessageId, Topic, MessageType)
            SELECT @MessageId, @Topic, 'telemetry'
            WHERE NOT EXISTS (SELECT 1 FROM dbo.ProcessedMessages WITH (UPDLOCK, HOLDLOCK) WHERE MessageId = @MessageId);
            """,
            new { telemetry.MessageId, Topic = topic },
            scope.Transaction,
            cancellationToken: cancellationToken));

        if (processed == 0)
        {
            await scope.CommitAsync(cancellationToken);
            return new TelemetryIngestionResult(telemetry.Serial, 0, WasDuplicate: true);
        }

        var device = await scope.Connection.QuerySingleOrDefaultAsync<DeviceRow>(new CommandDefinition(
            "SELECT Id FROM dbo.Devices WITH (UPDLOCK, HOLDLOCK) WHERE Serial = @Serial;",
            new { telemetry.Serial },
            scope.Transaction,
            cancellationToken: cancellationToken)) ?? throw new TelemetryIngestionException("Unknown device serial.");

        var capabilities = (await scope.Connection.QueryAsync<CapabilityRow>(new CommandDefinition(
            """
            SELECT Id, [Key], DataType, Kind, MinValue, MaxValue
            FROM dbo.Capabilities
            WHERE DeviceId = @DeviceId AND IsActive = 1;
            """,
            new { DeviceId = device.Id },
            scope.Transaction,
            cancellationToken: cancellationToken))).ToDictionary(row => row.Key, StringComparer.Ordinal);

        var triggeredCapabilities = new List<(Guid CapabilityId, TelemetryValue Value)>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reading in telemetry.Readings)
        {
            if (!seenKeys.Add(reading.CapabilityKey))
            {
                throw new TelemetryIngestionException($"Duplicate telemetry reading for capability '{reading.CapabilityKey}'.");
            }

            if (!capabilities.TryGetValue(reading.CapabilityKey, out var capability))
            {
                throw new TelemetryIngestionException($"Unknown active capability '{reading.CapabilityKey}'.");
            }

            if (!string.Equals(capability.Kind, "sensor", StringComparison.Ordinal))
            {
                throw new TelemetryIngestionException($"Capability '{reading.CapabilityKey}' is not a sensor.");
            }

            var value = ParseValue(reading.Value, capability);
            await InsertReadingAsync(scope, telemetry, device.Id, capability.Id, value, cancellationToken);
            await UpsertLatestAsync(scope, telemetry, device.Id, capability.Id, value, cancellationToken);
            triggeredCapabilities.Add((capability.Id, value));
        }

        foreach (var item in triggeredCapabilities)
        {
            await EvaluateAutomationRulesAsync(scope, telemetry.MessageId, item.CapabilityId, item.Value, cancellationToken);
        }

        await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.Devices
            SET Status = 'online', LastSeenAtUtc = @TimestampUtc, UpdatedAtUtc = SYSUTCDATETIME()
            WHERE Id = @DeviceId;
            """,
            new { DeviceId = device.Id, telemetry.TimestampUtc },
            scope.Transaction,
            cancellationToken: cancellationToken));

        await scope.CommitAsync(cancellationToken);
        return new TelemetryIngestionResult(telemetry.Serial, telemetry.Readings.Count, WasDuplicate: false);
    }

    private static void ValidateEnvelope(DeviceTelemetryMessage telemetry)
    {
        if (telemetry.MessageId == Guid.Empty)
        {
            throw new TelemetryIngestionException("Telemetry message id is required.");
        }

        if (!string.Equals(telemetry.SchemaVersion, DeviceMessageSerializer.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new TelemetryIngestionException("Unsupported telemetry schema version.");
        }

        if (!Domain.Devices.DeviceSerial.IsValid(telemetry.Serial))
        {
            throw new TelemetryIngestionException("Invalid device serial.");
        }

        if (telemetry.Readings.Count == 0)
        {
            throw new TelemetryIngestionException("Telemetry must contain at least one reading.");
        }
    }

    private static TelemetryValue ParseValue(JsonElement element, CapabilityRow capability)
    {
        return capability.DataType switch
        {
            "number" => ParseNumber(element, capability),
            "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False
                ? new TelemetryValue("boolean", null, element.GetBoolean(), null)
                : throw new TelemetryIngestionException($"Capability '{capability.Key}' requires a boolean value."),
            "string" => element.ValueKind == JsonValueKind.String
                ? new TelemetryValue("string", null, null, element.GetString())
                : throw new TelemetryIngestionException($"Capability '{capability.Key}' requires a string value."),
            _ => throw new TelemetryIngestionException($"Capability '{capability.Key}' has unsupported data type.")
        };
    }

    private static TelemetryValue ParseNumber(JsonElement element, CapabilityRow capability)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDecimal(out var number))
        {
            throw new TelemetryIngestionException($"Capability '{capability.Key}' requires a number value.");
        }

        if (capability.MinValue is not null && number < capability.MinValue)
        {
            throw new TelemetryIngestionException($"Capability '{capability.Key}' is below minimum value.");
        }

        if (capability.MaxValue is not null && number > capability.MaxValue)
        {
            throw new TelemetryIngestionException($"Capability '{capability.Key}' is above maximum value.");
        }

        return new TelemetryValue("number", number, null, null);
    }

    private static Task InsertReadingAsync(
        ISqlTransactionScope scope,
        DeviceTelemetryMessage telemetry,
        Guid deviceId,
        Guid capabilityId,
        TelemetryValue value,
        CancellationToken cancellationToken) =>
        scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.TelemetryRecords
                (MessageId, DeviceId, CapabilityId, ValueType, NumberValue, BooleanValue, StringValue, RecordedAtUtc)
            VALUES
                (@MessageId, @DeviceId, @CapabilityId, @ValueType, @NumberValue, @BooleanValue, @StringValue, @RecordedAtUtc);
            """,
            new
            {
                telemetry.MessageId,
                DeviceId = deviceId,
                CapabilityId = capabilityId,
                value.ValueType,
                value.NumberValue,
                value.BooleanValue,
                value.StringValue,
                RecordedAtUtc = telemetry.TimestampUtc
            },
            scope.Transaction,
            cancellationToken: cancellationToken));

    private static Task UpsertLatestAsync(
        ISqlTransactionScope scope,
        DeviceTelemetryMessage telemetry,
        Guid deviceId,
        Guid capabilityId,
        TelemetryValue value,
        CancellationToken cancellationToken) =>
        scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE dbo.DeviceLatestValues WITH (HOLDLOCK) AS target
            USING (SELECT @CapabilityId AS CapabilityId) AS source
            ON target.CapabilityId = source.CapabilityId
            WHEN MATCHED AND target.RecordedAtUtc <= @RecordedAtUtc THEN UPDATE SET
                DeviceId = @DeviceId,
                MessageId = @MessageId,
                ValueType = @ValueType,
                NumberValue = @NumberValue,
                BooleanValue = @BooleanValue,
                StringValue = @StringValue,
                RecordedAtUtc = @RecordedAtUtc,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT
                (CapabilityId, DeviceId, MessageId, ValueType, NumberValue, BooleanValue, StringValue, RecordedAtUtc)
            VALUES
                (@CapabilityId, @DeviceId, @MessageId, @ValueType, @NumberValue, @BooleanValue, @StringValue, @RecordedAtUtc);
            """,
            new
            {
                CapabilityId = capabilityId,
                DeviceId = deviceId,
                telemetry.MessageId,
                value.ValueType,
                value.NumberValue,
                value.BooleanValue,
                value.StringValue,
                RecordedAtUtc = telemetry.TimestampUtc
            },
            scope.Transaction,
            cancellationToken: cancellationToken));

    private static async Task EvaluateAutomationRulesAsync(
        ISqlTransactionScope scope,
        Guid triggerMessageId,
        Guid sourceCapabilityId,
        TelemetryValue value,
        CancellationToken cancellationToken)
    {
        var rules = await scope.Connection.QueryAsync<AutomationRuleRow>(new CommandDefinition(
            """
            SELECT
                ar.Id,
                ar.OwnerUserId,
                ar.TargetCapabilityId,
                targetDevice.Id AS TargetDeviceId,
                targetCapability.[Key] AS TargetCapabilityKey,
                ar.CommandName,
                ar.ComparisonOperator,
                ar.ThresholdValueType,
                ar.ThresholdNumberValue,
                ar.ThresholdBooleanValue,
                ar.ThresholdStringValue,
                ar.DesiredValueType,
                ar.DesiredNumberValue,
                ar.DesiredBooleanValue,
                ar.DesiredStringValue,
                ar.CooldownSeconds,
                ar.ActiveFromTime,
                ar.ActiveToTime,
                ar.ActiveDaysOfWeek,
                ar.LastConditionResult,
                ar.LastTriggeredAtUtc
            FROM dbo.AutomationRules ar WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.Capabilities targetCapability ON targetCapability.Id = ar.TargetCapabilityId
            INNER JOIN dbo.Devices targetDevice ON targetDevice.Id = targetCapability.DeviceId
            WHERE ar.SourceCapabilityId = @SourceCapabilityId
              AND ar.IsEnabled = 1;
            """,
            new { SourceCapabilityId = sourceCapabilityId },
            scope.Transaction,
            cancellationToken: cancellationToken));

        foreach (var rule in rules)
        {
            if (await ExecutionExistsAsync(scope, rule.Id, triggerMessageId, cancellationToken))
            {
                continue;
            }

            var conditionResult = EvaluateCondition(rule, value);
            var now = DateTimeOffset.UtcNow;
            if (!IsWithinSchedule(rule, now))
            {
                await InsertAutomationExecutionAsync(scope, rule.Id, triggerMessageId, null, "skipped", "outside_schedule", cancellationToken);
                await UpdateRuleStateAsync(scope, rule.Id, conditionResult, null, cancellationToken);
                continue;
            }

            var edgeTriggered = conditionResult && rule.LastConditionResult != true;
            var lastTriggeredAtUtc = rule.LastTriggeredAtUtc is null
                ? (DateTimeOffset?)null
                : new DateTimeOffset(DateTime.SpecifyKind(rule.LastTriggeredAtUtc.Value, DateTimeKind.Utc));
            var cooldownReady = lastTriggeredAtUtc is null || lastTriggeredAtUtc.Value.AddSeconds(rule.CooldownSeconds) <= now;

            if (edgeTriggered && cooldownReady)
            {
                var commandId = Guid.NewGuid();
                await InsertAutomationCommandAsync(scope, rule, commandId, now.AddSeconds(30), cancellationToken);
                await InsertThresholdNotificationAsync(scope, rule, cancellationToken);
                await InsertAutomationExecutionAsync(scope, rule.Id, triggerMessageId, commandId, "triggered", null, cancellationToken);
                await UpdateRuleStateAsync(scope, rule.Id, conditionResult, now, cancellationToken);
                continue;
            }

            var reason = conditionResult
                ? edgeTriggered ? "cooldown" : "condition_already_true"
                : "condition_false";
            await InsertAutomationExecutionAsync(scope, rule.Id, triggerMessageId, null, "skipped", reason, cancellationToken);
            await UpdateRuleStateAsync(scope, rule.Id, conditionResult, null, cancellationToken);
        }
    }

    private static async Task<bool> ExecutionExistsAsync(ISqlTransactionScope scope, Guid ruleId, Guid triggerMessageId, CancellationToken cancellationToken)
    {
        var exists = await scope.Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.AutomationExecutions WHERE AutomationRuleId = @RuleId AND TriggerMessageId = @TriggerMessageId;",
            new { RuleId = ruleId, TriggerMessageId = triggerMessageId },
            scope.Transaction,
            cancellationToken: cancellationToken));
        return exists > 0;
    }

    private static bool EvaluateCondition(AutomationRuleRow rule, TelemetryValue value)
    {
        if (!string.Equals(rule.ThresholdValueType, value.ValueType, StringComparison.Ordinal))
        {
            return false;
        }

        return value.ValueType switch
        {
            "number" => Compare(rule.ComparisonOperator, value.NumberValue!.Value.CompareTo(rule.ThresholdNumberValue!.Value)),
            "boolean" => Compare(rule.ComparisonOperator, value.BooleanValue!.Value.CompareTo(rule.ThresholdBooleanValue!.Value)),
            "string" => Compare(rule.ComparisonOperator, string.Compare(value.StringValue, rule.ThresholdStringValue, StringComparison.Ordinal)),
            _ => false
        };
    }

    private static bool Compare(string comparisonOperator, int comparison) => comparisonOperator switch
    {
        ">" => comparison > 0,
        ">=" => comparison >= 0,
        "<" => comparison < 0,
        "<=" => comparison <= 0,
        "==" => comparison == 0,
        "!=" => comparison != 0,
        _ => false
    };

    private static bool IsWithinSchedule(AutomationRuleRow rule, DateTimeOffset now)
    {
        if (rule.ActiveDaysOfWeek is not null)
        {
            var dayBit = 1 << (int)now.UtcDateTime.DayOfWeek;
            if ((rule.ActiveDaysOfWeek.Value & dayBit) == 0)
            {
                return false;
            }
        }

        if (rule.ActiveFromTime is null && rule.ActiveToTime is null)
        {
            return true;
        }

        var current = now.UtcDateTime.TimeOfDay;
        var from = rule.ActiveFromTime ?? TimeSpan.Zero;
        var to = rule.ActiveToTime ?? new TimeSpan(23, 59, 59);
        return from <= to
            ? current >= from && current <= to
            : current >= from || current <= to;
    }

    private static Task InsertAutomationCommandAsync(
        ISqlTransactionScope scope,
        AutomationRuleRow rule,
        Guid commandId,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken) =>
        scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.DeviceCommands
                (Id, DeviceId, CapabilityId, OwnerUserId, AutomationRuleId, CommandName,
                 DesiredValueType, DesiredNumberValue, DesiredBooleanValue, DesiredStringValue,
                 Source, Status, ExpiresAtUtc)
            VALUES
                (@CommandId, @DeviceId, @CapabilityId, @OwnerUserId, @AutomationRuleId, @CommandName,
                 @DesiredValueType, @DesiredNumberValue, @DesiredBooleanValue, @DesiredStringValue,
                 'automation', 'pending', @ExpiresAtUtc);
            """,
            new
            {
                CommandId = commandId,
                DeviceId = rule.TargetDeviceId,
                CapabilityId = rule.TargetCapabilityId,
                rule.OwnerUserId,
                AutomationRuleId = rule.Id,
                rule.CommandName,
                rule.DesiredValueType,
                rule.DesiredNumberValue,
                rule.DesiredBooleanValue,
                rule.DesiredStringValue,
                ExpiresAtUtc = expiresAtUtc
            },
            scope.Transaction,
            cancellationToken: cancellationToken));

    private static Task InsertThresholdNotificationAsync(
        ISqlTransactionScope scope,
        AutomationRuleRow rule,
        CancellationToken cancellationToken) =>
        scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.Notifications (OwnerUserId, DeviceId, CapabilityId, Type, Severity, Title, Message)
            SELECT @OwnerUserId, @TargetDeviceId, @TargetCapabilityId, 'threshold_triggered', 'info',
                   'Automation threshold triggered', @Message
            WHERE NOT EXISTS (
                SELECT 1
                FROM dbo.Notifications WITH (UPDLOCK, HOLDLOCK)
                WHERE OwnerUserId = @OwnerUserId
                  AND CapabilityId = @TargetCapabilityId
                  AND Type = 'threshold_triggered'
                  AND CreatedAtUtc >= DATEADD(minute, -10, SYSUTCDATETIME())
            );
            """,
            new
            {
                rule.OwnerUserId,
                rule.TargetDeviceId,
                rule.TargetCapabilityId,
                Message = $"Automation rule {rule.Id} triggered command {rule.CommandName} for {rule.TargetCapabilityKey}."
            },
            scope.Transaction,
            cancellationToken: cancellationToken));

    private static Task InsertAutomationExecutionAsync(
        ISqlTransactionScope scope,
        Guid ruleId,
        Guid triggerMessageId,
        Guid? commandId,
        string outcome,
        string? reason,
        CancellationToken cancellationToken) =>
        scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.AutomationExecutions (AutomationRuleId, TriggerMessageId, CommandId, Outcome, Reason)
            SELECT @RuleId, @TriggerMessageId, @CommandId, @Outcome, @Reason
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.AutomationExecutions WITH (UPDLOCK, HOLDLOCK)
                WHERE AutomationRuleId = @RuleId AND TriggerMessageId = @TriggerMessageId
            );
            """,
            new { RuleId = ruleId, TriggerMessageId = triggerMessageId, CommandId = commandId, Outcome = outcome, Reason = reason },
            scope.Transaction,
            cancellationToken: cancellationToken));

    private static Task UpdateRuleStateAsync(
        ISqlTransactionScope scope,
        Guid ruleId,
        bool conditionResult,
        DateTimeOffset? triggeredAtUtc,
        CancellationToken cancellationToken) =>
        scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.AutomationRules
            SET LastConditionResult = @ConditionResult,
                LastTriggeredAtUtc = COALESCE(@TriggeredAtUtc, LastTriggeredAtUtc),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE Id = @RuleId;
            """,
            new { RuleId = ruleId, ConditionResult = conditionResult, TriggeredAtUtc = triggeredAtUtc },
            scope.Transaction,
            cancellationToken: cancellationToken));

    private sealed record DeviceRow(Guid Id);

    private sealed record CapabilityRow(Guid Id, string Key, string DataType, string Kind, decimal? MinValue, decimal? MaxValue);

    private sealed record TelemetryValue(string ValueType, decimal? NumberValue, bool? BooleanValue, string? StringValue);

    private sealed record AutomationRuleRow(
        Guid Id,
        Guid OwnerUserId,
        Guid TargetCapabilityId,
        Guid TargetDeviceId,
        string TargetCapabilityKey,
        string CommandName,
        string ComparisonOperator,
        string ThresholdValueType,
        decimal? ThresholdNumberValue,
        bool? ThresholdBooleanValue,
        string? ThresholdStringValue,
        string DesiredValueType,
        decimal? DesiredNumberValue,
        bool? DesiredBooleanValue,
        string? DesiredStringValue,
        int CooldownSeconds,
        TimeSpan? ActiveFromTime,
        TimeSpan? ActiveToTime,
        byte? ActiveDaysOfWeek,
        bool? LastConditionResult,
        DateTime? LastTriggeredAtUtc);
}
