namespace IoTPlatform.Application.Notifications;

public interface INotificationRepository
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int take, CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(Guid userId, long id, CancellationToken cancellationToken = default);
}
