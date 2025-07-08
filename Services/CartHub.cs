using Microsoft.AspNetCore.SignalR;

public class CartHub : Hub
{
    public async Task JoinCartGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"cart_{userId}");
    }

    public async Task LeaveCartGroup(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"cart_{userId}");
    }
}