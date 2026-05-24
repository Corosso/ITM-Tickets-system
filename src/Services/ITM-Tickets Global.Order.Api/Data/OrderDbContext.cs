using ITM_Tickets_Global.Order.Api.Sagas;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace ITM_Tickets_Global.Order.Api.Data;

/// <summary>
/// DbContext que persiste el estado del Saga de Órdenes. Si el Pod del
/// Order.Api se cae a mitad de un flujo, al reiniciar MassTransit recupera
/// el estado desde PostgreSQL y continúa donde se quedó.
/// </summary>
public class OrderDbContext : SagaDbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get
        {
            yield return new OrderStateMap();
        }
    }

    public DbSet<OrderRecord> Orders => Set<OrderRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OrderRecord>(e =>
        {
            e.ToTable("orders");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(200);
        });
    }
}

public class OrderRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = "Processing";
    public DateTime CreatedAt { get; set; }
    public string? ReservationId { get; set; }
}
