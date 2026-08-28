using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PqrsPlatform.Api.Hubs;

[Authorize]
public class TicketsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tenantId));

        await base.OnConnectedAsync();
    }

    public static string GroupName(string tenantId) => $"tenant:{tenantId}";
}
