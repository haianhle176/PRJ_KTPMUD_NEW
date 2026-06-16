using Dapper;
using IoTPlatform.Application.Notifications;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Notifications;

public sealed class SqlNotificationRepository(IDbConnectionFactory connectionFactory) : INotificationRepository
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(
        Guid userId,
        bool unreadOnly,
        int take,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT TOP (@Take)
                Id, OwnerUserId, DeviceId, CapabilityId, Type, Severity, Title, Message, IsRead, CreatedAtUtc
            FROM dbo.Notifications
            WHERE OwnerUserId = @UserId
              AND (@UnreadOnly = 0 OR IsRead = 0)
            ORDER BY CreatedAtUtc DESC, Id DESC;
            """,
            new { UserId = userId, UnreadOnly = unreadOnly, Take = take },
            cancellationToken: cancellationToken));
        return rows.Select(ToNotification).ToArray();
    }

    public async Task<bool> MarkReadAsync(Guid userId, long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.Notifications
            SET IsRead = 1
            WHERE Id = @Id AND OwnerUserId = @UserId;
            """,
            new { Id = id, UserId = userId },
            cancellationToken: cancellationToken));
        return affected == 1;
    }

    private static NotificationDto ToNotification(dynamic row)
    {
        DateTime createdAtUtc = row.CreatedAtUtc;
        return new NotificationDto(
            row.Id,
            row.OwnerUserId,
            row.DeviceId,
            row.CapabilityId,
            row.Type,
            row.Severity,
            row.Title,
            row.Message,
            row.IsRead,
            new DateTimeOffset(DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)));
    }
}
