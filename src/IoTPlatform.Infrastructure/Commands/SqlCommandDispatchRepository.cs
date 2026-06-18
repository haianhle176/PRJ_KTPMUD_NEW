using System.Text.Json;
using Dapper;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Commands;

public sealed class SqlCommandDispatchRepository(ISqlTransactionScopeFactory transactionScopeFactory) : ICommandDispatchRepository
{
    public async Task<IReadOnlyList<PendingCommand>> GetPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        await using var scope = await transactionScopeFactory.BeginAsync(cancellationToken);
        await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.DeviceCommands
            SET Status = 'timed_out', FailureCode = 'expired', FailureMessage = 'Command expired before completion.'
            WHERE Status IN ('pending', 'sent', 'accepted') AND ExpiresAtUtc <= SYSUTCDATETIME();
            """,
            transaction: scope.Transaction,
            cancellationToken: cancellationToken));

        var rows = await scope.Connection.QueryAsync(new CommandDefinition(
            """
            SELECT TOP (@Take)
                dc.Id,
                d.Serial,
                c.[Key] AS CapabilityKey,
                dc.CommandName,
                dc.DesiredValueType,
                dc.DesiredNumberValue,
                dc.DesiredBooleanValue,
                dc.DesiredStringValue,
                dc.ExpiresAtUtc
            FROM dbo.DeviceCommands dc WITH (READPAST)
            INNER JOIN dbo.Devices d ON d.Id = dc.DeviceId
            INNER JOIN dbo.Capabilities c ON c.Id = dc.CapabilityId
            WHERE dc.ExpiresAtUtc > SYSUTCDATETIME()
              AND (
                    dc.Status = 'pending'
                    OR (dc.Status = 'sent' AND dc.SentAtUtc <= DATEADD(second, -5, SYSUTCDATETIME()))
                  )
            ORDER BY dc.CreatedAtUtc;
            """,
            new { Take = take },
            scope.Transaction,
            cancellationToken: cancellationToken));
        await scope.CommitAsync(cancellationToken);
        return rows.Select(ToPending).ToArray();
    }

    public async Task<bool> MarkSentAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        await using var scope = await transactionScopeFactory.BeginAsync(cancellationToken);
        var affected = await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.DeviceCommands
            SET Status = 'sent', SentAtUtc = SYSUTCDATETIME()
            WHERE Id = @CommandId AND Status IN ('pending', 'sent');
            """,
            new { CommandId = commandId },
            scope.Transaction,
            cancellationToken: cancellationToken));
        await scope.CommitAsync(cancellationToken);
        return affected == 1;
    }

    public async Task<CommandAckResult> ApplyAcknowledgementAsync(DeviceCommandAcknowledgementMessage acknowledgement, CancellationToken cancellationToken = default)
    {
        await using var scope = await transactionScopeFactory.BeginAsync(cancellationToken);
        var inserted = await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.CommandAcknowledgements
                (MessageId, CommandId, Status, ReportedValueType, ReportedNumberValue, ReportedBooleanValue, ReportedStringValue, ErrorCode, ErrorMessage)
            SELECT @MessageId, @CommandId, @Status, @ReportedValueType, @ReportedNumberValue, @ReportedBooleanValue, @ReportedStringValue, @ErrorCode, @ErrorMessage
            WHERE NOT EXISTS (SELECT 1 FROM dbo.CommandAcknowledgements WITH (UPDLOCK, HOLDLOCK) WHERE MessageId = @MessageId);
            """,
            CreateAckParameters(acknowledgement),
            scope.Transaction,
            cancellationToken: cancellationToken));

        if (inserted == 0)
        {
            await scope.CommitAsync(cancellationToken);
            return new CommandAckResult(acknowledgement.CommandId, acknowledgement.Status, WasDuplicate: true);
        }

        var updateSql = acknowledgement.Status switch
        {
            "accepted" => """
                UPDATE dbo.DeviceCommands
                SET Status = CASE WHEN Status IN ('pending', 'sent') THEN 'accepted' ELSE Status END,
                    AcceptedAtUtc = COALESCE(AcceptedAtUtc, SYSUTCDATETIME())
                WHERE Id = @CommandId;
                """,
            "completed" => """
                UPDATE dbo.DeviceCommands
                SET Status = CASE WHEN Status IN ('pending', 'sent', 'accepted') THEN 'completed' ELSE Status END,
                    CompletedAtUtc = COALESCE(CompletedAtUtc, SYSUTCDATETIME())
                WHERE Id = @CommandId;
                """,
            "failed" => """
                UPDATE dbo.DeviceCommands
                SET Status = CASE WHEN Status IN ('pending', 'sent', 'accepted') THEN 'failed' ELSE Status END,
                    FailureCode = @ErrorCode,
                    FailureMessage = @ErrorMessage
                WHERE Id = @CommandId;
                """,
            _ => throw new InvalidOperationException("Unsupported acknowledgement status.")
        };

        await scope.Connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            new { acknowledgement.CommandId, acknowledgement.ErrorCode, acknowledgement.ErrorMessage },
            scope.Transaction,
            cancellationToken: cancellationToken));

        if (acknowledgement.Status == "completed" && acknowledgement.ReportedValue is not null)
        {
            var reportedValue = CommandAckValue.FromJson(acknowledgement.ReportedValue.Value);
            await scope.Connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT dbo.TelemetryRecords
                    (MessageId, DeviceId, CapabilityId, ValueType, NumberValue, BooleanValue, StringValue, RecordedAtUtc)
                SELECT
                    @MessageId, dc.DeviceId, dc.CapabilityId, @ValueType, @NumberValue, @BooleanValue, @StringValue, @RecordedAtUtc
                FROM dbo.DeviceCommands dc
                INNER JOIN dbo.Devices d ON d.Id = dc.DeviceId AND d.Serial = @Serial
                LEFT JOIN dbo.DeviceLatestValues latest ON latest.CapabilityId = dc.CapabilityId
                WHERE dc.Id = @CommandId
                  AND (latest.CapabilityId IS NULL OR @RecordedAtUtc >= latest.RecordedAtUtc)
                  AND (
                        latest.CapabilityId IS NULL
                        OR latest.ValueType <> @ValueType
                        OR (@ValueType = 'number' AND (latest.NumberValue IS NULL OR latest.NumberValue <> @NumberValue))
                        OR (@ValueType = 'boolean' AND (latest.BooleanValue IS NULL OR latest.BooleanValue <> @BooleanValue))
                        OR (@ValueType = 'string' AND (latest.StringValue IS NULL OR latest.StringValue <> @StringValue))
                      );
                """,
                new
                {
                    acknowledgement.CommandId,
                    acknowledgement.Serial,
                    acknowledgement.MessageId,
                    RecordedAtUtc = acknowledgement.TimestampUtc,
                    ValueType = reportedValue.ValueType,
                    NumberValue = reportedValue.NumberValue,
                    BooleanValue = reportedValue.BooleanValue,
                    StringValue = reportedValue.StringValue
                },
                scope.Transaction,
                cancellationToken: cancellationToken));

            await scope.Connection.ExecuteAsync(new CommandDefinition(
                """
                MERGE dbo.DeviceLatestValues WITH (HOLDLOCK) AS target
                USING (
                    SELECT dc.CapabilityId, dc.DeviceId
                    FROM dbo.DeviceCommands dc
                    INNER JOIN dbo.Devices d ON d.Id = dc.DeviceId AND d.Serial = @Serial
                    WHERE dc.Id = @CommandId
                ) AS source
                ON target.CapabilityId = source.CapabilityId
                WHEN MATCHED AND @RecordedAtUtc >= target.RecordedAtUtc THEN
                    UPDATE SET
                        DeviceId = source.DeviceId,
                        MessageId = @MessageId,
                        ValueType = @ValueType,
                        NumberValue = @NumberValue,
                        BooleanValue = @BooleanValue,
                        StringValue = @StringValue,
                        RecordedAtUtc = @RecordedAtUtc,
                        UpdatedAtUtc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (CapabilityId, DeviceId, MessageId, ValueType, NumberValue, BooleanValue, StringValue, RecordedAtUtc)
                    VALUES (source.CapabilityId, source.DeviceId, @MessageId, @ValueType, @NumberValue, @BooleanValue, @StringValue, @RecordedAtUtc);
                """,
                new
                {
                    acknowledgement.CommandId,
                    acknowledgement.Serial,
                    acknowledgement.MessageId,
                    RecordedAtUtc = acknowledgement.TimestampUtc,
                    ValueType = reportedValue.ValueType,
                    NumberValue = reportedValue.NumberValue,
                    BooleanValue = reportedValue.BooleanValue,
                    StringValue = reportedValue.StringValue
                },
                scope.Transaction,
                cancellationToken: cancellationToken));
        }

        await scope.CommitAsync(cancellationToken);
        return new CommandAckResult(acknowledgement.CommandId, acknowledgement.Status, WasDuplicate: false);
    }

    private static object CreateAckParameters(DeviceCommandAcknowledgementMessage acknowledgement)
    {
        var value = acknowledgement.ReportedValue is null ? null : CommandAckValue.FromJson(acknowledgement.ReportedValue.Value);
        return new
        {
            acknowledgement.MessageId,
            acknowledgement.CommandId,
            acknowledgement.Status,
            ReportedValueType = value?.ValueType,
            ReportedNumberValue = value?.NumberValue,
            ReportedBooleanValue = value?.BooleanValue,
            ReportedStringValue = value?.StringValue,
            acknowledgement.ErrorCode,
            acknowledgement.ErrorMessage
        };
    }

    private static PendingCommand ToPending(dynamic row)
    {
        DateTime expiresAtUtc = row.ExpiresAtUtc;
        return new PendingCommand(
            row.Id,
            row.Serial,
            row.CapabilityKey,
            row.CommandName,
            row.DesiredValueType,
            row.DesiredNumberValue,
            row.DesiredBooleanValue,
            row.DesiredStringValue,
            new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)));
    }

    private sealed record CommandAckValue(string ValueType, decimal? NumberValue, bool? BooleanValue, string? StringValue)
    {
        public static CommandAckValue FromJson(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number when element.TryGetDecimal(out var number) => new CommandAckValue("number", number, null, null),
                JsonValueKind.True => new CommandAckValue("boolean", null, true, null),
                JsonValueKind.False => new CommandAckValue("boolean", null, false, null),
                JsonValueKind.String => new CommandAckValue("string", null, null, element.GetString()),
                _ => throw new InvalidOperationException("Reported value must be a number, boolean or string.")
            };
        }
    }
}
