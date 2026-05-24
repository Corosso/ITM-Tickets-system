using ITM_Tickets_Global.Order.Api.Sagas;
using ITM_Tickets_Global.Shared.Protos;
using MassTransit;

namespace ITM_Tickets_Global.Order.Api.Consumers;

/// <summary>
/// Cierra el ciclo del Saga: cuando se publica `InventoryReserveRequest`,
/// este consumer invoca el gRPC de Inventory.Api y publica el resultado
/// (`InventoryReserved` o `InventoryReservationFailed`) que el Saga espera.
///
/// Si Inventory.Api está caído, MassTransit reintenta con backoff exponencial
/// y el mensaje queda en la cola de RabbitMQ hasta que el servicio se
/// recupere, garantizando entrega exactly-once una vez restaurado.
/// </summary>
public class InventoryRequestConsumer : IConsumer<InventoryReserveRequest>
{
    private readonly InventoryService.InventoryServiceClient _inventory;
    private readonly ILogger<InventoryRequestConsumer> _logger;

    public InventoryRequestConsumer(InventoryService.InventoryServiceClient inventory, ILogger<InventoryRequestConsumer> logger)
    {
        _inventory = inventory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InventoryReserveRequest> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Procesando reserva de inventario para orden {OrderId}", msg.OrderId);

        var request = new ReserveSeatsRequest { OrderId = msg.OrderId.ToString() };
        foreach (var item in msg.Items)
        {
            request.Seats.Add(new SeatRequest
            {
                EventId = item.EventId.ToString(),
                Section = item.Section,
                Row = item.Row,
                SeatNumber = item.SeatNumber,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });
        }

        try
        {
            var response = await _inventory.ReserveSeatsAsync(request);

            if (response.Success)
            {
                await context.Publish(new InventoryReservedEvent
                {
                    OrderId = msg.OrderId,
                    ReservationId = response.ReservationId,
                    Items = msg.Items
                });
            }
            else
            {
                await context.Publish(new InventoryReservationFailedEvent
                {
                    OrderId = msg.OrderId,
                    Reason = response.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invocando gRPC ReserveSeats. Reintentando vía RabbitMQ.");
            throw;
        }
    }
}
