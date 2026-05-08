using MassTransit;

namespace ITM_Tickets_Global.Order.Api.Sagas;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public List<OrderItemData> Items { get; set; } = [];
    public string? ReservationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? FailureReason { get; set; }
}

public record OrderItemData
{
    public Guid EventId { get; set; }
    public string Section { get; set; } = string.Empty;
    public int Row { get; set; }
    public int SeatNumber { get; set; }
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
}

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State Created { get; private set; }
    public State AwaitingInventory { get; private set; }
    public State Confirmed { get; private set; }
    public State Cancelled { get; private set; }

    public Event<OrderCreatedEvent> OrderCreated { get; private set; }
    public Event<InventoryReservedEvent> InventoryReserved { get; private set; }
    public Event<InventoryReservationFailedEvent> InventoryReservationFailed { get; private set; }

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReserved, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReservationFailed, x => x.CorrelateById(m => m.Message.OrderId));

        Initially(
            When(OrderCreated)
                .Then(context =>
                {
                    context.Saga.OrderId = context.Message.OrderId;
                    context.Saga.UserId = context.Message.UserId;
                    context.Saga.Email = context.Message.Email;
                    context.Saga.CreatedAt = context.Message.CreatedAt;
                    context.Saga.Items = context.Message.Items
                        .Select(i => new OrderItemData
                        {
                            EventId = i.EventId,
                            Section = i.Section,
                            Row = i.Row,
                            SeatNumber = i.SeatNumber,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice
                        }).ToList();
                })
                .Publish(context => new InventoryReserveRequest
                {
                    OrderId = context.Saga.OrderId,
                    Items = context.Saga.Items.Select(i => new Shared.Events.OrderItemMessage(
                        i.EventId, i.Section, i.Row, i.SeatNumber, i.Quantity, i.UnitPrice)).ToList()
                })
                .TransitionTo(AwaitingInventory)
        );

        During(AwaitingInventory,
            When(InventoryReserved)
                .Then(context =>
                {
                    context.Saga.ReservationId = context.Message.ReservationId;
                    context.Saga.ConfirmedAt = DateTime.UtcNow;
                })
                .Publish(context => new OrderConfirmedEvent
                {
                    OrderId = context.Saga.OrderId,
                    UserId = context.Saga.UserId,
                    Email = context.Saga.Email,
                    ConfirmedAt = context.Saga.ConfirmedAt ?? DateTime.UtcNow,
                    Tickets = context.Saga.Items.Select((i, idx) => new Shared.Events.TicketIssued(
                        Guid.NewGuid(), i.EventId, $"Event-{i.EventId.ToString()[..8]}",
                        i.Section, i.Row, i.SeatNumber, $"QR-{Guid.NewGuid().ToString()[..12]}")).ToList()
                })
                .TransitionTo(Confirmed),

            When(InventoryReservationFailed)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.CancelledAt = DateTime.UtcNow;
                })
                .Publish(context => new OrderCancelledEvent
                {
                    OrderId = context.Saga.OrderId,
                    UserId = context.Saga.UserId,
                    Reason = context.Message.Reason,
                    CancelledAt = context.Saga.CancelledAt ?? DateTime.UtcNow
                })
                .TransitionTo(Cancelled)
        );
    }
}

public record OrderCreatedEvent
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public List<Shared.Events.OrderItemMessage> Items { get; init; } = [];
}

public record InventoryReserveRequest
{
    public Guid OrderId { get; init; }
    public List<Shared.Events.OrderItemMessage> Items { get; init; } = [];
}

public record InventoryReservedEvent
{
    public Guid OrderId { get; init; }
    public string ReservationId { get; init; } = string.Empty;
    public List<Shared.Events.OrderItemMessage> Items { get; init; } = [];
}

public record InventoryReservationFailedEvent
{
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record OrderConfirmedEvent
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public DateTime ConfirmedAt { get; init; }
    public List<Shared.Events.TicketIssued> Tickets { get; init; } = [];
}

public record OrderCancelledEvent
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime CancelledAt { get; init; }
}
