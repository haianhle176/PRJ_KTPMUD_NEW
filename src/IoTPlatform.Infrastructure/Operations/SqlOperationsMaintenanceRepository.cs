using Dapper;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Operations;

public sealed class SqlOperationsMaintenanceRepository(IDbConnectionFactory connectionFactory) : IOperationsMaintenanceRepository
{
    public async Task<int> AggregateTelemetryHourlyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE dbo.TelemetryHourlyAggregates WITH (HOLDLOCK) AS target
            USING (
                SELECT
                    tr.CapabilityId,
                    DATEADD(hour, DATEDIFF(hour, 0, tr.RecordedAtUtc), 0) AS BucketStartUtc,
                    tr.DeviceId,
                    COUNT(1) AS SampleCount,
                    MIN(tr.NumberValue) AS MinNumberValue,
                    MAX(tr.NumberValue) AS MaxNumberValue,
                    AVG(tr.NumberValue) AS AvgNumberValue
                FROM dbo.TelemetryRecords tr
                WHERE tr.ValueType = 'number'
                  AND tr.RecordedAtUtc >= DATEADD(day, -2, SYSUTCDATETIME())
                GROUP BY tr.CapabilityId, DATEADD(hour, DATEDIFF(hour, 0, tr.RecordedAtUtc), 0), tr.DeviceId
            ) AS source
            ON target.CapabilityId = source.CapabilityId
               AND target.BucketStartUtc = source.BucketStartUtc
            WHEN MATCHED THEN UPDATE SET
                DeviceId = source.DeviceId,
                SampleCount = source.SampleCount,
                MinNumberValue = source.MinNumberValue,
                MaxNumberValue = source.MaxNumberValue,
                AvgNumberValue = source.AvgNumberValue,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT
                (CapabilityId, BucketStartUtc, DeviceId, SampleCount, MinNumberValue, MaxNumberValue, AvgNumberValue)
            VALUES
                (source.CapabilityId, source.BucketStartUtc, source.DeviceId, source.SampleCount,
                 source.MinNumberValue, source.MaxNumberValue, source.AvgNumberValue);
            """,
            cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteExpiredOperationalDataAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var telemetry = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE dbo.TelemetryRecords WHERE RecordedAtUtc < DATEADD(day, -30, SYSUTCDATETIME());",
            cancellationToken: cancellationToken));
        var processedMessages = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE dbo.ProcessedMessages WHERE ProcessedAtUtc < DATEADD(day, -7, SYSUTCDATETIME());",
            cancellationToken: cancellationToken));
        var readNotifications = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE dbo.Notifications WHERE IsRead = 1 AND CreatedAtUtc < DATEADD(day, -30, SYSUTCDATETIME());",
            cancellationToken: cancellationToken));
        var realtimeOutbox = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE dbo.RealtimeOutbox WHERE DispatchedAtUtc < DATEADD(day, -7, SYSUTCDATETIME());",
            cancellationToken: cancellationToken));
        return telemetry + processedMessages + readNotifications + realtimeOutbox;
    }
}
