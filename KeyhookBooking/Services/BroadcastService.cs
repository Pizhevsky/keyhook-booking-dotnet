using KeyhookBooking.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace KeyhookBooking.Services;

public interface IBroadcastService
{
    Task SendAsync(string eventType, object payload);
}

public class BroadcastService(IHubContext<BookingHub> hub) : IBroadcastService
{
    public async Task SendAsync(string eventType, object payload)
    {
        await hub.Clients.All.SendAsync("message", new { type = eventType, payload });
    }
}
