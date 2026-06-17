namespace IoTPlatform.Application.Notifications;

public sealed class NotificationService(INotificationRepository repository) : INotificationService
{
    public Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int take, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (take is < 1 or > 200)
        {
            throw new ArgumentException("Take must be between 1 and 200.", nameof(take));
        }

        return repository.ListAsync(userId, unreadOnly, take, cancellationToken);
    }

    public async Task MarkReadAsync(Guid userId, long id, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (!await repository.MarkReadAsync(userId, id, cancellationToken))
        {
            throw new ArgumentException("Notification was not found.", nameof(id));
        }
    }

    public Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return repository.MarkAllReadAsync(userId, cancellationToken);
    }
}
