using Dapper;
using IoTPlatform.Application.Audit;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Audit;

public sealed class SqlAuditLogRepository(IDbConnectionFactory connectionFactory) : IAuditLogRepository
{
    public async Task WriteAsync(Guid? userId, string action, string entityType, string? entityId, string? detailsJson = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.AuditLogs (UserId, Action, EntityType, EntityId, DetailsJson)
            VALUES (@UserId, @Action, @EntityType, @EntityId, @DetailsJson);
            """,
            new { UserId = userId, Action = action, EntityType = entityType, EntityId = entityId, DetailsJson = detailsJson },
            cancellationToken: cancellationToken));
    }
}
