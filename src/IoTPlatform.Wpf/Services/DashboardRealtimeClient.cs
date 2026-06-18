using IoTPlatform.Application.Realtime;
using Microsoft.AspNetCore.SignalR.Client;

namespace IoTPlatform.Wpf.Services;

public sealed class DashboardRealtimeClient : IAsyncDisposable
{
    private readonly HubConnection _connection;

    public DashboardRealtimeClient(IoTPlatformApiClient apiClient)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(apiClient.BaseAddress, "hubs/dashboard"), options =>
            {
                options.AccessTokenProvider = apiClient.GetAccessTokenAsync;
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _connection.On<DashboardChangedEvent>("DashboardChanged", changed =>
            Changed is null ? Task.CompletedTask : Changed(changed));
        _connection.Reconnecting += _ => SetStateAsync("Reconnecting");
        _connection.Reconnected += async _ =>
        {
            await SetStateAsync("Connected");
            if (Reconnected is not null)
            {
                await Reconnected();
            }
        };
        _connection.Closed += _ => SetStateAsync("Disconnected");
    }

    public event Func<DashboardChangedEvent, Task>? Changed;
    public event Func<Task>? Reconnected;
    public event Func<string, Task>? StateChanged;

    public string State { get; private set; } = "Disconnected";

    public async Task StartAsync()
    {
        if (_connection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
        {
            return;
        }

        try
        {
            await _connection.StartAsync();
            await SetStateAsync("Connected");
        }
        catch
        {
            await SetStateAsync("Disconnected");
        }
    }

    public async Task StopAsync()
    {
        if (_connection.State != HubConnectionState.Disconnected)
        {
            await _connection.StopAsync();
        }
        await SetStateAsync("Disconnected");
    }

    private async Task SetStateAsync(string state)
    {
        State = state;
        if (StateChanged is not null)
        {
            await StateChanged(state);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
