using Dapper;
using IoTPlatform.Application.Telemetry;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Telemetry;

public sealed class SqlTelemetryQueryRepository(IDbConnectionFactory connectionFactory) : ITelemetryQueryRepository
{
    public async Task<IReadOnlyList<TelemetryValueDto>> GetLatestValuesAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT
                c.Id AS CapabilityId,
                c.[Key] AS CapabilityKey,
                c.DisplayName,
                lv.ValueType,
                lv.NumberValue,
                lv.BooleanValue,
                lv.StringValue,
                lv.RecordedAtUtc
            FROM dbo.Devices d
            INNER JOIN dbo.Capabilities c ON c.DeviceId = d.Id AND c.IsActive = 1
            INNER JOIN dbo.DeviceLatestValues lv ON lv.CapabilityId = c.Id
            WHERE d.Id = @DeviceId AND d.OwnerUserId = @UserId
            ORDER BY c.[Key];
            """,
            new { DeviceId = deviceId, UserId = userId },
            cancellationToken: cancellationToken));
        return rows.Select(ToLatest).ToArray();
    }

    public async Task<(IReadOnlyList<TelemetryHistoryItemDto> Items, int TotalCount)> GetHistoryAsync(
        Guid userId,
        Guid deviceId,
        string? capabilityKey,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        const string whereClause = """
            d.Id = @DeviceId
            AND d.OwnerUserId = @UserId
            AND (@CapabilityKey IS NULL OR c.[Key] = @CapabilityKey)
            AND (@FromUtc IS NULL OR tr.RecordedAtUtc >= @FromUtc)
            AND (@ToUtc IS NULL OR tr.RecordedAtUtc <= @ToUtc)
            """;
        var parameters = new
        {
            DeviceId = deviceId,
            UserId = userId,
            CapabilityKey = capabilityKey,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Skip = skip,
            Take = take
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"""
            SELECT COUNT(*)
            FROM dbo.TelemetryRecords tr
            INNER JOIN dbo.Capabilities c ON c.Id = tr.CapabilityId
            INNER JOIN dbo.Devices d ON d.Id = tr.DeviceId
            WHERE {whereClause};
            """,
            parameters,
            cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync(new CommandDefinition(
            $"""
            SELECT
                tr.Id,
                c.Id AS CapabilityId,
                c.[Key] AS CapabilityKey,
                c.DisplayName,
                tr.ValueType,
                tr.NumberValue,
                tr.BooleanValue,
                tr.StringValue,
                tr.RecordedAtUtc,
                tr.ReceivedAtUtc
            FROM dbo.TelemetryRecords tr
            INNER JOIN dbo.Capabilities c ON c.Id = tr.CapabilityId
            INNER JOIN dbo.Devices d ON d.Id = tr.DeviceId
            WHERE {whereClause}
            ORDER BY tr.RecordedAtUtc DESC, tr.Id DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """,
            parameters,
            cancellationToken: cancellationToken));

        return (rows.Select(ToHistory).ToArray(), totalCount);
    }

    private static TelemetryValueDto ToLatest(dynamic row)
    {
        DateTime recordedAtUtc = row.RecordedAtUtc;
        return new TelemetryValueDto(
            row.CapabilityId,
            row.CapabilityKey,
            row.DisplayName,
            row.ValueType,
            row.NumberValue,
            row.BooleanValue,
            row.StringValue,
            new DateTimeOffset(DateTime.SpecifyKind(recordedAtUtc, DateTimeKind.Utc)));
    }

    private static TelemetryHistoryItemDto ToHistory(dynamic row)
    {
        DateTime recordedAtUtc = row.RecordedAtUtc;
        DateTime receivedAtUtc = row.ReceivedAtUtc;
        return new TelemetryHistoryItemDto(
            row.Id,
            row.CapabilityId,
            row.CapabilityKey,
            row.DisplayName,
            row.ValueType,
            row.NumberValue,
            row.BooleanValue,
            row.StringValue,
            new DateTimeOffset(DateTime.SpecifyKind(recordedAtUtc, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(receivedAtUtc, DateTimeKind.Utc)));
    }
}
