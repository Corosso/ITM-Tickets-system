using MassTransit;
using Microsoft.AspNetCore.SignalR;
using ITM_Tickets_Global.Notification.Api.Hubs;
using ITM_Tickets_Global.Shared.Events;

namespace ITM_Tickets_Global.Notification.Api.Consumers;

public class OrderConfirmedConsumer : IConsumer<OrderConfirmedEvent>
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<OrderConfirmedConsumer> _logger;

    public OrderConfirmedConsumer(IHubContext<NotificationHub> hubContext, ILogger<OrderConfirmedConsumer> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderConfirmedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Order {OrderId} confirmed. Notifying user {UserId} via SignalR",
            message.OrderId, message.UserId);

        await _hubContext.Clients.Group($"order-{message.OrderId}")
            .SendAsync("OrderConfirmed", new
            {
                message.OrderId,
                message.ConfirmedAt,
                message.Tickets
            });

        foreach (var ticket in message.Tickets)
        {
            await context.Publish(new TicketReadyEvent
            {
                TicketId = ticket.TicketId,
                OrderId = message.OrderId,
                EventId = ticket.EventId,
                EventName = ticket.EventName,
                Section = ticket.Section,
                Row = ticket.Row,
                SeatNumber = ticket.SeatNumber,
                QrCode = ticket.QrCode,
                Email = message.Email
            });
        }
    }
}

public class TicketReadyConsumer : IConsumer<TicketReadyEvent>
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<TicketReadyConsumer> _logger;

    public TicketReadyConsumer(IHubContext<NotificationHub> hubContext, ILogger<TicketReadyConsumer> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TicketReadyEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Ticket {TicketId} ready for Order {OrderId}", message.TicketId, message.OrderId);

        await _hubContext.Clients.Group($"order-{message.OrderId}")
            .SendAsync("TicketReady", new
            {
                message.TicketId,
                message.OrderId,
                message.EventId,
                message.EventName,
                message.Section,
                message.Row,
                message.SeatNumber,
                message.QrCode
            });
    }
}

// OrderConfirmedEvent y TicketIssued ahora viven en ITM_Tickets_Global.Shared.Events
// (compartidos con Order.Api). El consumer arriba usa Shared.Events.OrderConfirmedEvent
// y para los tickets usamos Shared.Events.TicketIssued (record positional).

public record TicketReadyEvent
{
    public Guid TicketId { get; init; }
    public Guid OrderId { get; init; }
    public Guid EventId { get; init; }
    public string EventName { get; init; } = string.Empty;
    public string Section { get; init; } = string.Empty;
    public int Row { get; init; }
    public int SeatNumber { get; init; }
    public string QrCode { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}
