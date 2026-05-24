using ITM_Tickets_Global.Order.Api.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ITM_Tickets_Global.Order.Api.Services;

/// <summary>
/// Orquesta la creación de órdenes. Persiste el registro en PostgreSQL y
/// dispara el Saga de inventario vía RabbitMQ.
/// </summary>
public class OrderService
{
    private readonly IBus _bus;
    private readonly OrderDbContext _db;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IBus bus, OrderDbContext db, ILogger<OrderService> logger)
    {
        _bus = bus;
        _db = db;
        _logger = logger;
    }

    public async Task<(bool Success, Guid OrderId, string? Error)> CreateOrderAsync(CreateOrderRequest request)
    {
        if (request.Items.Count == 0)
        {
            return (false, Guid.Empty, "La orden debe tener al menos un asiento");
        }

        var orderId = Guid.NewGuid();
        var items = request.Items.Select(i => new Shared.Events.OrderItemMessage(
            i.EventId, i.Section, i.Row, i.SeatNumber, i.Quantity, i.UnitPrice
        )).ToList();

        _db.Orders.Add(new OrderRecord
        {
            Id = orderId,
            UserId = request.UserId,
            Email = request.Email,
            Status = "Processing",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _bus.Publish(new Sagas.OrderCreatedEvent
        {
            OrderId = orderId,
            UserId = request.UserId,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            Items = items
        });

        _logger.LogInformation("Orden {OrderId} creada y publicada al Saga. Usuario={UserId}", orderId, request.UserId);

        return (true, orderId, null);
    }

    public async Task<OrderStatusResponse?> GetOrderStatusAsync(Guid orderId)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return null;

        return new OrderStatusResponse
        {
            OrderId = orderId,
            Status = order.Status,
            UpdatedAt = order.CreatedAt,
            Tickets = null
        };
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
