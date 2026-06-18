using Dapper;
using IoTPlatform.Application.Realtime;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Realtime;

public sealed class SqlRealtimeOutboxRepository(IDbConnectionFactory connectionFactory) : IRealtimeOutboxRepository
{
    public async Task<IReadOnlyList<RealtimeOutboxItem>> GetPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<OutboxRow>(new CommandDefinition(
            """
            SELECT TOP (@Take) Id, EventId, OwnerUserId, Kind, DeviceId, EntityId, OccurredAtUtc, AttemptCount
            FROM dbo.RealtimeOutbox WITH (READPAST)
            WHERE DispatchedAtUtc IS NULL AND NextAttemptAtUtc <= SYSUTCDATETIME()
            ORDER BY Id;
            """,
            new { Take = Math.Clamp(take, 1, 500) },
            cancellationToken: cancellationToken));
        return rows.Select(row => new RealtimeOutboxItem(
            row.Id,
            row.OwnerUserId,
            new DashboardChangedEvent(row.EventId, row.Kind, row.DeviceId, row.EntityId, new DateTimeOffset(DateTime.SpecifyKind(row.OccurredAtUtc, DateTimeKind.Utc))),
            row.AttemptCount)).ToArray();
    }

    public async Task MarkDispatchedAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.RealtimeOutbox SET DispatchedAtUtc = SYSUTCDATETIME(), LastError = NULL WHERE Id = @Id AND DispatchedAtUtc IS NULL;",
            new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task MarkFailedAsync(long id, string error, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.RealtimeOutbox
            SET AttemptCount = AttemptCount + 1,
                NextAttemptAtUtc = DATEADD(second, CASE WHEN AttemptCount >= 5 THEN 30 ELSE POWER(2, AttemptCount) END, SYSUTCDATETIME()),
                LastError = LEFT(@Error, 1000)
            WHERE Id = @Id AND DispatchedAtUtc IS NULL;
            """,
            new { Id = id, Error = error }, cancellationToken: cancellationToken));
    }

    private sealed record OutboxRow(long Id, Guid EventId, Guid? OwnerUserId, string Kind, Guid? DeviceId, string? EntityId, DateTime OccurredAtUtc, int AttemptCount);
}
