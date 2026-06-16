using Dapper;
using IoTPlatform.Application.Automation;
using IoTPlatform.Application.Commands;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Automation;

public sealed class SqlAutomationRuleRepository(IDbConnectionFactory connectionFactory) : IAutomationRuleRepository
{
    public async Task<AutomationRuleDto?> CreateAsync(
        Guid userId,
        CreateAutomationRuleRequestDto request,
        CommandValue thresholdValue,
        CommandValue desiredValue,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var ruleId = Guid.NewGuid();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.AutomationRules
                (Id, OwnerUserId, Name, SourceCapabilityId, ComparisonOperator,
                 ThresholdValueType, ThresholdNumberValue, ThresholdBooleanValue, ThresholdStringValue,
                 TargetCapabilityId, CommandName, DesiredValueType, DesiredNumberValue, DesiredBooleanValue, DesiredStringValue,
                 IsEnabled, CooldownSeconds)
            SELECT
                @RuleId, @UserId, @Name, sourceCapability.Id, @ComparisonOperator,
                @ThresholdValueType, @ThresholdNumberValue, @ThresholdBooleanValue, @ThresholdStringValue,
                targetCapability.Id, @CommandName, @DesiredValueType, @DesiredNumberValue, @DesiredBooleanValue, @DesiredStringValue,
                @IsEnabled, @CooldownSeconds
            FROM dbo.Devices sourceDevice
            INNER JOIN dbo.Capabilities sourceCapability ON sourceCapability.DeviceId = sourceDevice.Id AND sourceCapability.IsActive = 1
            INNER JOIN dbo.Devices targetDevice ON targetDevice.Id = @TargetDeviceId AND targetDevice.OwnerUserId = @UserId
            INNER JOIN dbo.Capabilities targetCapability ON targetCapability.DeviceId = targetDevice.Id AND targetCapability.IsActive = 1
            WHERE sourceDevice.Id = @SourceDeviceId
              AND sourceDevice.OwnerUserId = @UserId
              AND sourceCapability.[Key] = @SourceCapabilityKey
              AND sourceCapability.Kind = 'sensor'
              AND sourceCapability.DataType = @ThresholdValueType
              AND targetCapability.[Key] = @TargetCapabilityKey
              AND targetCapability.Kind = 'actuator'
              AND targetCapability.IsWritable = 1
              AND targetCapability.DataType = @DesiredValueType
              AND targetCapability.SupportedCommandsJson LIKE @SupportedCommand;
            """,
            Parameters(ruleId, userId, request, thresholdValue, desiredValue),
            cancellationToken: cancellationToken));
        return affected == 1 ? await GetAsync(userId, ruleId, cancellationToken) : null;
    }

    public async Task<IReadOnlyList<AutomationRuleDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(new CommandDefinition(
            SelectSql + " WHERE ar.OwnerUserId = @UserId ORDER BY ar.Name;",
            new { UserId = userId },
            cancellationToken: cancellationToken));
        return rows.Select(ToRule).ToArray();
    }

    public async Task<AutomationRuleDto?> GetAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            SelectSql + " WHERE ar.OwnerUserId = @UserId AND ar.Id = @RuleId;",
            new { UserId = userId, RuleId = ruleId },
            cancellationToken: cancellationToken));
        return row is null ? null : ToRule(row);
    }

    public async Task<AutomationRuleDto?> UpdateAsync(
        Guid userId,
        Guid ruleId,
        UpdateAutomationRuleRequestDto request,
        CommandValue thresholdValue,
        CommandValue desiredValue,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE ar
            SET Name = @Name,
                ComparisonOperator = @ComparisonOperator,
                ThresholdValueType = @ThresholdValueType,
                ThresholdNumberValue = @ThresholdNumberValue,
                ThresholdBooleanValue = @ThresholdBooleanValue,
                ThresholdStringValue = @ThresholdStringValue,
                CommandName = @CommandName,
                DesiredValueType = @DesiredValueType,
                DesiredNumberValue = @DesiredNumberValue,
                DesiredBooleanValue = @DesiredBooleanValue,
                DesiredStringValue = @DesiredStringValue,
                IsEnabled = @IsEnabled,
                CooldownSeconds = @CooldownSeconds,
                UpdatedAtUtc = SYSUTCDATETIME()
            FROM dbo.AutomationRules ar
            INNER JOIN dbo.Capabilities sourceCapability ON sourceCapability.Id = ar.SourceCapabilityId
            INNER JOIN dbo.Capabilities targetCapability ON targetCapability.Id = ar.TargetCapabilityId
            WHERE ar.Id = @RuleId
              AND ar.OwnerUserId = @UserId
              AND sourceCapability.DataType = @ThresholdValueType
              AND targetCapability.DataType = @DesiredValueType
              AND targetCapability.SupportedCommandsJson LIKE @SupportedCommand;
            """,
            new
            {
                RuleId = ruleId,
                UserId = userId,
                request.Name,
                request.ComparisonOperator,
                ThresholdValueType = thresholdValue.ValueType,
                ThresholdNumberValue = thresholdValue.NumberValue,
                ThresholdBooleanValue = thresholdValue.BooleanValue,
                ThresholdStringValue = thresholdValue.StringValue,
                CommandName = request.CommandName.Trim().ToUpperInvariant(),
                DesiredValueType = desiredValue.ValueType,
                DesiredNumberValue = desiredValue.NumberValue,
                DesiredBooleanValue = desiredValue.BooleanValue,
                DesiredStringValue = desiredValue.StringValue,
                request.IsEnabled,
                request.CooldownSeconds,
                SupportedCommand = $"%\"{request.CommandName.Trim().ToUpperInvariant()}\"%"
            },
            cancellationToken: cancellationToken));
        return affected == 1 ? await GetAsync(userId, ruleId, cancellationToken) : null;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE dbo.AutomationRules WHERE Id = @RuleId AND OwnerUserId = @UserId;",
            new { RuleId = ruleId, UserId = userId },
            cancellationToken: cancellationToken));
        return affected == 1;
    }

    public async Task<IReadOnlyList<AutomationExecutionDto>> ListExecutionsAsync(Guid userId, Guid ruleId, int take, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT TOP (@Take) ae.Id, ae.AutomationRuleId, ae.TriggerMessageId, ae.CommandId, ae.Outcome, ae.Reason, ae.EvaluatedAtUtc
            FROM dbo.AutomationExecutions ae
            INNER JOIN dbo.AutomationRules ar ON ar.Id = ae.AutomationRuleId
            WHERE ar.OwnerUserId = @UserId AND ar.Id = @RuleId
            ORDER BY ae.EvaluatedAtUtc DESC, ae.Id DESC;
            """,
            new { UserId = userId, RuleId = ruleId, Take = take },
            cancellationToken: cancellationToken));
        return rows.Select(ToExecution).ToArray();
    }

    private const string SelectSql = """
        SELECT
            ar.Id,
            ar.Name,
            sourceDevice.Id AS SourceDeviceId,
            sourceDevice.Serial AS SourceDeviceSerial,
            sourceCapability.[Key] AS SourceCapabilityKey,
            ar.ComparisonOperator,
            ar.ThresholdValueType,
            ar.ThresholdNumberValue,
            ar.ThresholdBooleanValue,
            ar.ThresholdStringValue,
            targetDevice.Id AS TargetDeviceId,
            targetDevice.Serial AS TargetDeviceSerial,
            targetCapability.[Key] AS TargetCapabilityKey,
            ar.CommandName,
            ar.DesiredValueType,
            ar.DesiredNumberValue,
            ar.DesiredBooleanValue,
            ar.DesiredStringValue,
            ar.IsEnabled,
            ar.CooldownSeconds,
            ar.LastConditionResult,
            ar.LastTriggeredAtUtc,
            ar.CreatedAtUtc,
            ar.UpdatedAtUtc
        FROM dbo.AutomationRules ar
        INNER JOIN dbo.Capabilities sourceCapability ON sourceCapability.Id = ar.SourceCapabilityId
        INNER JOIN dbo.Devices sourceDevice ON sourceDevice.Id = sourceCapability.DeviceId
        INNER JOIN dbo.Capabilities targetCapability ON targetCapability.Id = ar.TargetCapabilityId
        INNER JOIN dbo.Devices targetDevice ON targetDevice.Id = targetCapability.DeviceId
        """;

    private static object Parameters(
        Guid ruleId,
        Guid userId,
        CreateAutomationRuleRequestDto request,
        CommandValue thresholdValue,
        CommandValue desiredValue) => new
        {
            RuleId = ruleId,
            UserId = userId,
            request.Name,
            SourceDeviceId = request.SourceDeviceId,
            SourceCapabilityKey = request.SourceCapabilityKey,
            request.ComparisonOperator,
            ThresholdValueType = thresholdValue.ValueType,
            ThresholdNumberValue = thresholdValue.NumberValue,
            ThresholdBooleanValue = thresholdValue.BooleanValue,
            ThresholdStringValue = thresholdValue.StringValue,
            TargetDeviceId = request.TargetDeviceId,
            TargetCapabilityKey = request.TargetCapabilityKey,
            CommandName = request.CommandName.Trim().ToUpperInvariant(),
            DesiredValueType = desiredValue.ValueType,
            DesiredNumberValue = desiredValue.NumberValue,
            DesiredBooleanValue = desiredValue.BooleanValue,
            DesiredStringValue = desiredValue.StringValue,
            request.IsEnabled,
            request.CooldownSeconds,
            SupportedCommand = $"%\"{request.CommandName.Trim().ToUpperInvariant()}\"%"
        };

    private static AutomationRuleDto ToRule(dynamic row)
    {
        DateTime? lastTriggeredAtUtc = row.LastTriggeredAtUtc;
        DateTime createdAtUtc = row.CreatedAtUtc;
        DateTime updatedAtUtc = row.UpdatedAtUtc;
        return new AutomationRuleDto(
            row.Id,
            row.Name,
            row.SourceDeviceId,
            row.SourceDeviceSerial,
            row.SourceCapabilityKey,
            row.ComparisonOperator,
            row.ThresholdValueType,
            row.ThresholdNumberValue,
            row.ThresholdBooleanValue,
            row.ThresholdStringValue,
            row.TargetDeviceId,
            row.TargetDeviceSerial,
            row.TargetCapabilityKey,
            row.CommandName,
            row.DesiredValueType,
            row.DesiredNumberValue,
            row.DesiredBooleanValue,
            row.DesiredStringValue,
            row.IsEnabled,
            row.CooldownSeconds,
            row.LastConditionResult,
            lastTriggeredAtUtc is null ? null : Utc(lastTriggeredAtUtc.Value),
            Utc(createdAtUtc),
            Utc(updatedAtUtc));
    }

    private static AutomationExecutionDto ToExecution(dynamic row)
    {
        DateTime evaluatedAtUtc = row.EvaluatedAtUtc;
        return new AutomationExecutionDto(row.Id, row.AutomationRuleId, row.TriggerMessageId, row.CommandId, row.Outcome, row.Reason, Utc(evaluatedAtUtc));
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
