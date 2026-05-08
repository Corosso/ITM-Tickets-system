using MassTransit;

namespace ITM_Tickets_Global.Order.Api.Services;

public class OrderService
{
    private readonly IBus _bus;
    private readonly ILogger<OrderService> _logger;
    private static readonly Dictionary<Guid, (string Status, DateTime Created)> Orders = [];

    public OrderService(IBus bus, ILogger<OrderService> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public async Task<(bool Success, Guid OrderId, string? Error)> CreateOrderAsync(CreateOrderRequest request)
    {
        var orderId = Guid.NewGuid();

        var items = request.Items.Select(i => new Shared.Events.OrderItemMessage(
            i.EventId, i.Section, i.Row, i.SeatNumber, i.Quantity, i.UnitPrice
        )).ToList();

        Orders[orderId] = ("Processing", DateTime.UtcNow);

        await _bus.Publish(new Sagas.OrderCreatedEvent
        {
            OrderId = orderId,
            UserId = request.UserId,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            Items = items
        });

        _logger.LogInformation("Order {OrderId} created and published to SAGA. User: {UserId}", orderId, request.UserId);

        return (true, orderId, null);
    }

    public Task<OrderStatusResponse?> GetOrderStatusAsync(Guid orderId)
    {
        if (Orders.TryGetValue(orderId, out var order))
        {
            return Task.FromResult<OrderStatusResponse?>(new OrderStatusResponse
            {
                OrderId = orderId,
                Status = order.Status,
                UpdatedAt = order.Created,
                Tickets = null
            });
        }
        return Task.FromResult<OrderStatusResponse?>(null);
    }
}

public record CreateOrderRequest
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public List<OrderItemRequest> Items { get; init; } = [];
}

public record OrderItemRequest
{
    public Guid EventId { get; init; }
    public string Section { get; init; } = string.Empty;
    public int Row { get; init; }
    public int SeatNumber { get; init; }
    public int Quantity { get; init; }
    public double UnitPrice { get; init; }
}

public record OrderStatusResponse
{
    public Guid OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
    public List<object>? Tickets { get; init; }
}
