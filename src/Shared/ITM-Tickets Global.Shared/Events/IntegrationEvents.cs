namespace ITM_Tickets_Global.Shared.Events;

public interface OrderCreated
{
    Guid OrderId { get; }
    Guid UserId { get; }
    string Email { get; }
    DateTime CreatedAt { get; }
    List<OrderItemMessage> Items { get; }
}

public interface OrderConfirmed
{
    Guid OrderId { get; }
    Guid UserId { get; }
    string Email { get; }
    DateTime ConfirmedAt { get; }
    List<TicketIssued> Tickets { get; }
}

public interface OrderCancelled
{
    Guid OrderId { get; }
    Guid UserId { get; }
    string Reason { get; }
    DateTime CancelledAt { get; }
}

public interface InventoryReserved
{
    Guid OrderId { get; }
    string ReservationId { get; }
    List<OrderItemMessage> Items { get; }
}

public interface InventoryReservationFailed
{
    Guid OrderId { get; }
    string Reason { get; }
}

public interface TicketReady
{
    Guid TicketId { get; }
    Guid OrderId { get; }
    Guid EventId { get; }
    string EventName { get; }
    string Section { get; }
    int Row { get; }
    int SeatNumber { get; }
    string QrCode { get; }
    string Email { get; }
}

public record OrderItemMessage(
    Guid EventId,
    string Section,
    int Row,
    int SeatNumber,
    int Quantity,
    double UnitPrice
);

public record TicketIssued(
    Guid TicketId,
    Guid EventId,
    string EventName,
    string Section,
    int Row,
    int SeatNumber,
    string QrCode
);

/// <summary>
/// Evento publicado por Order.Api (Saga) cuando la orden se confirma.
/// Consumido por Notification.Api para empujar la confirmación vía SignalR
/// al cliente MAUI. AMBOS servicios DEBEN usar este tipo desde acá para
/// que MassTransit los routee al mismo exchange en RabbitMQ.
/// </summary>
public record OrderConfirmedEvent
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public DateTime ConfirmedAt { get; init; }
    public List<TicketIssued> Tickets { get; init; } = [];
}
