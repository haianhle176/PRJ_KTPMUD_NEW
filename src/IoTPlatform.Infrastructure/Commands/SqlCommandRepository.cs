using Dapper;
using IoTPlatform.Application.Commands;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Commands;

public sealed class SqlCommandRepository(IDbConnectionFactory connectionFactory) : ICommandRepository
{
    public async Task<CommandDto?> CreateManualAsync(
        Guid userId,
        Guid deviceId,
        string capabilityKey,
        string commandName,
        CommandValue desiredValue,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var supportedCommand = $"%\"{commandName}\"%";
        var commandId = Guid.NewGuid();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.DeviceCommands
                (Id, DeviceId, CapabilityId, OwnerUserId, AutomationRuleId, CommandName,
                 DesiredValueType, DesiredNumberValue, DesiredBooleanValue, DesiredStringValue,
                 Source, Status, ExpiresAtUtc)
            SELECT
                @CommandId,
                d.Id,
                c.Id,
                @UserId,
                NULL,
                @CommandName,
                @ValueType,
                @NumberValue,
                @BooleanValue,
                @StringValue,
                'manual',
                'pending',
                @ExpiresAtUtc
            FROM dbo.Devices d
            INNER JOIN dbo.Capabilities c ON c.DeviceId = d.Id AND c.IsActive = 1
            WHERE d.Id = @DeviceId
              AND d.OwnerUserId = @UserId
              AND c.[Key] = @CapabilityKey
              AND c.Kind = 'actuator'
              AND c.IsWritable = 1
              AND c.DataType = @ValueType
              AND c.SupportedCommandsJson LIKE @SupportedCommand;
            """,
            new
            {
                CommandId = commandId,
                UserId = userId,
                DeviceId = deviceId,
                CapabilityKey = capabilityKey,
                CommandName = commandName,
                desiredValue.ValueType,
                desiredValue.NumberValue,
                desiredValue.BooleanValue,
                desiredValue.StringValue,
                ExpiresAtUtc = expiresAtUtc,
                SupportedCommand = supportedCommand
            },
            cancellationToken: cancellationToken));
        return affected == 1 ? await GetAsync(userId, commandId, cancellationToken) : null;
    }

    public async Task<CommandDto?> GetAsync(Guid userId, Guid commandId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            """
            SELECT
                dc.Id,
                dc.DeviceId,
                d.Serial,
                c.[Key] AS CapabilityKey,
                dc.CommandName,
                dc.DesiredValueType,
                dc.DesiredNumberValue,
                dc.DesiredBooleanValue,
                dc.DesiredStringValue,
                dc.Source,
                dc.Status,
                dc.CreatedAtUtc,
                dc.SentAtUtc,
                dc.AcceptedAtUtc,
                dc.CompletedAtUtc,
                dc.ExpiresAtUtc,
                dc.FailureCode,
                dc.FailureMessage
            FROM dbo.DeviceCommands dc
            INNER JOIN dbo.Devices d ON d.Id = dc.DeviceId
            INNER JOIN dbo.Capabilities c ON c.Id = dc.CapabilityId
            WHERE dc.Id = @CommandId AND dc.OwnerUserId = @UserId;
            """,
            new { CommandId = commandId, UserId = userId },
            cancellationToken: cancellationToken));
        return row is null ? null : ToCommand(row);
    }

    internal static CommandDto ToCommand(dynamic row)
    {
        DateTime createdAtUtc = row.CreatedAtUtc;
        DateTime? sentAtUtc = row.SentAtUtc;
        DateTime? acceptedAtUtc = row.AcceptedAtUtc;
        DateTime? completedAtUtc = row.CompletedAtUtc;
        DateTime expiresAtUtc = row.ExpiresAtUtc;

        return new CommandDto(
            row.Id,
            row.DeviceId,
            row.Serial,
            row.CapabilityKey,
            row.CommandName,
            row.DesiredValueType,
            row.DesiredNumberValue,
            row.DesiredBooleanValue,
            row.DesiredStringValue,
            row.Source,
            row.Status,
            Utc(createdAtUtc),
            sentAtUtc is null ? null : Utc(sentAtUtc.Value),
            acceptedAtUtc is null ? null : Utc(acceptedAtUtc.Value),
            completedAtUtc is null ? null : Utc(completedAtUtc.Value),
            Utc(expiresAtUtc),
            row.FailureCode,
            row.FailureMessage);
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
