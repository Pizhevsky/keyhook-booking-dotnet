using Microsoft.AspNetCore.SignalR;

namespace KeyhookBooking.Hubs;

public class BookingHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"SignalR client connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"SignalR client disconnected: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }
}
