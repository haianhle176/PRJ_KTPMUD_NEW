using IoTPlatform.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace IoTPlatform.Api.Realtime;

public sealed class RealtimeOutboxDispatcher(
    IRealtimeOutboxRepository repository,
    IHubContext<DashboardHub> hubContext,
    ILogger<RealtimeOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var items = await repository.GetPendingAsync(100, stoppingToken);
                foreach (var item in items)
                {
                    try
                    {
                        var clients = item.OwnerUserId is Guid userId
                            ? hubContext.Clients.Group(RealtimeGroups.User(userId))
                            : hubContext.Clients.Group(RealtimeGroups.Authenticated);
                        await clients.SendAsync("DashboardChanged", item.Event, stoppingToken);
                        await repository.MarkDispatchedAsync(item.Id, stoppingToken);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogWarning(exception, "Could not dispatch realtime outbox event {EventId}.", item.Event.EventId);
                        await repository.MarkFailedAsync(item.Id, exception.Message, stoppingToken);
                    }
                }

                await Task.Delay(items.Count == 0 ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(100), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Realtime outbox dispatch loop failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
