using Dapper;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Automation;

public sealed class SqlScheduledAutomationRepository(IDbConnectionFactory connectionFactory) : IScheduledAutomationRepository
{
    public async Task<int> TriggerDueScheduleOnlyRulesAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var rules = await connection.QueryAsync<ScheduledRuleRow>(new CommandDefinition(
                """
                SELECT
                    ar.Id,
                    ar.OwnerUserId,
                    ar.TargetCapabilityId,
                    targetDevice.Id AS TargetDeviceId,
                    targetCapability.[Key] AS TargetCapabilityKey,
                    ar.CommandName,
                    ar.DesiredValueType,
                    ar.DesiredNumberValue,
                    ar.DesiredBooleanValue,
                    ar.DesiredStringValue,
                    ar.CooldownSeconds,
                    ar.ActiveFromTime,
                    ar.ActiveToTime,
                    ar.ActiveDaysOfWeek,
                    ar.LastTriggeredAtUtc
                FROM dbo.AutomationRules ar WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.Capabilities targetCapability ON targetCapability.Id = ar.TargetCapabilityId
                INNER JOIN dbo.Devices targetDevice ON targetDevice.Id = targetCapability.DeviceId
                WHERE ar.SourceCapabilityId IS NULL
                  AND ar.IsEnabled = 1;
                """,
                transaction: transaction,
                cancellationToken: cancellationToken));

            var triggered = 0;
            foreach (var rule in rules)
            {
                if (!IsWithinSchedule(rule, now) || !IsCooldownReady(rule, now))
                {
                    continue;
                }

                var commandId = Guid.NewGuid();
                await InsertAutomationCommandAsync(connection, transaction, rule, commandId, now.AddSeconds(30), cancellationToken);
                await InsertAutomationExecutionAsync(connection, transaction, rule.Id, commandId, "triggered", null, cancellationToken);
                await UpdateRuleTriggeredAsync(connection, transaction, rule.Id, now, cancellationToken);
                triggered++;
            }

            await transaction.CommitAsync(cancellationToken);
            return triggered;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static bool IsCooldownReady(ScheduledRuleRow rule, DateTimeOffset now)
    {
        if (rule.LastTriggeredAtUtc is null)
        {
            return true;
        }

        var lastTriggered = new DateTimeOffset(DateTime.SpecifyKind(rule.LastTriggeredAtUtc.Value, DateTimeKind.Utc));
        return lastTriggered.AddSeconds(rule.CooldownSeconds) <= now;
    }

    private static bool IsWithinSchedule(ScheduledRuleRow rule, DateTimeOffset now)
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
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        ScheduledRuleRow rule,
        Guid commandId,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
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
            transaction,
            cancellationToken: cancellationToken));

    private static Task InsertAutomationExecutionAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid ruleId,
        Guid commandId,
        string outcome,
        string? reason,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.AutomationExecutions (AutomationRuleId, TriggerMessageId, CommandId, Outcome, Reason)
            VALUES (@RuleId, NULL, @CommandId, @Outcome, @Reason);
            """,
            new { RuleId = ruleId, CommandId = commandId, Outcome = outcome, Reason = reason },
            transaction,
            cancellationToken: cancellationToken));

    private static Task UpdateRuleTriggeredAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid ruleId,
        DateTimeOffset triggeredAtUtc,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.AutomationRules
            SET LastConditionResult = 1,
                LastTriggeredAtUtc = @TriggeredAtUtc,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE Id = @RuleId;
            """,
            new { RuleId = ruleId, TriggeredAtUtc = triggeredAtUtc },
            transaction,
            cancellationToken: cancellationToken));

    private sealed record ScheduledRuleRow(
        Guid Id,
        Guid OwnerUserId,
        Guid TargetCapabilityId,
        Guid TargetDeviceId,
        string TargetCapabilityKey,
        string CommandName,
        string DesiredValueType,
        decimal? DesiredNumberValue,
        bool? DesiredBooleanValue,
        string? DesiredStringValue,
        int CooldownSeconds,
        TimeSpan? ActiveFromTime,
        TimeSpan? ActiveToTime,
        byte? ActiveDaysOfWeek,
        DateTime? LastTriggeredAtUtc);
}
