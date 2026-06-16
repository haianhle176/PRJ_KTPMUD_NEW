namespace IoTPlatform.Application.Audit;

public interface IAuditLogRepository
{
    Task WriteAsync(Guid? userId, string action, string entityType, string? entityId, string? detailsJson = null, CancellationToken cancellationToken = default);
}
