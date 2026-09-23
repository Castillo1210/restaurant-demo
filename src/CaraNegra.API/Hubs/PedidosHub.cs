using Microsoft.AspNetCore.SignalR;

namespace CaraNegra.API.Hubs;

public class PedidosHub : Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public override async Task OnConnectedAsync()
    {
        // Unirse automáticamente al grupo según el rol del usuario
        var roles = Context.User?.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
        
        if (roles?.Contains("MOZO") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Mozo");
        }
        if (roles?.Contains("CAJERO") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Cajero");
        }
        if (roles?.Contains("ADMIN") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admin");
        }
        if (roles?.Contains("COCINA") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Cocina");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Mozo");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Cajero");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admin");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Cocina");
        await base.OnDisconnectedAsync(exception);
    }
}