namespace IoTPlatform.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int take, CancellationToken cancellationToken = default);

    Task MarkReadAsync(Guid userId, long id, CancellationToken cancellationToken = default);
}
