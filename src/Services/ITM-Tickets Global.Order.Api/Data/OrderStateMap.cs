using System.Text.Json;
using ITM_Tickets_Global.Order.Api.Sagas;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITM_Tickets_Global.Order.Api.Data;

/// <summary>
/// Mapping EF Core para el estado del Saga. La columna `CurrentState` guarda
/// "Created", "AwaitingInventory", "Confirmed" o "Cancelled". Los Items se
/// serializan a JSON para persistir el detalle de la orden.
/// </summary>
public class OrderStateMap : SagaClassMap<OrderState>
{
    protected override void Configure(EntityTypeBuilder<OrderState> entity, ModelBuilder modelBuilder)
    {
        entity.ToTable("order_sagas");
        entity.Property(x => x.CurrentState).HasMaxLength(64);
        entity.Property(x => x.Email).HasMaxLength(200);
        entity.Property(x => x.FailureReason).HasMaxLength(500);

        // Items: List<OrderItemData> persistido como JSON en una columna text.
        entity.Property(x => x.Items)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<OrderItemData>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("text");
    }
}
