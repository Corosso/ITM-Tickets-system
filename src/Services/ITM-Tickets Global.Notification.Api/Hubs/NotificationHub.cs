using Microsoft.AspNetCore.SignalR;

namespace ITM_Tickets_Global.Notification.Api.Hubs;

public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? Context.ConnectionId;
        _logger.LogInformation("Client connected: {UserId}", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier ?? Context.ConnectionId;
        _logger.LogInformation("Client disconnected: {UserId}", userId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToOrder(string orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to order {OrderId}", Context.ConnectionId, orderId);
    }
}
