using IoTPlatform.Application.Realtime;

namespace IoTPlatform.Infrastructure.Realtime;

public interface IRealtimeOutboxRepository
{
    Task<IReadOnlyList<RealtimeOutboxItem>> GetPendingAsync(int take, CancellationToken cancellationToken = default);
    Task MarkDispatchedAsync(long id, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(long id, string error, CancellationToken cancellationToken = default);
}

public sealed record RealtimeOutboxItem(long Id, Guid? OwnerUserId, DashboardChangedEvent Event, int AttemptCount);
