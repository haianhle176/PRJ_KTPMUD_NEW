using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IoTPlatform.Api.Realtime;

[Authorize]
public sealed class DashboardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var subject = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.User(userId));
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Authenticated);
        await base.OnConnectedAsync();
    }
}

public static class RealtimeGroups
{
    public const string Authenticated = "authenticated";
    public static string User(Guid userId) => $"user:{userId:N}";
}
