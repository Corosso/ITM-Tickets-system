namespace ITM_Tickets_Global.Shared.Dtos;

public record CreateOrderRequest(
    Guid UserId,
    string Email,
    string PhoneNumber,
    List<OrderItemRequest> Items
);

public record OrderItemRequest(
    Guid EventId,
    string Section,
    int Row,
    int SeatNumber,
    int Quantity,
    double UnitPrice
);

public record CreateOrderResponse(
    Guid OrderId,
    string Status,
    string Message,
    DateTime CreatedAt
);

public record OrderStatusResponse(
    Guid OrderId,
    string Status,
    DateTime UpdatedAt,
    List<TicketInfo>? Tickets
);

public record TicketInfo(
    Guid TicketId,
    Guid EventId,
    string EventName,
    string Section,
    int Row,
    int SeatNumber,
    string QrCode,
    DateTime EventDate
);

public record PriceResponse(
    Guid EventId,
    string Section,
    double CurrentPrice,
    double BasePrice,
    double Multiplier,
    DateTime LastUpdated
);

public record SearchResponse(
    Guid Id,
    string Name,
    string Description,
    string Venue,
    string City,
    DateTime StartDate,
    double Score
);
