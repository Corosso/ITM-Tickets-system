using ITM_Tickets_Global.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITM_Tickets_Global.Inventory.Api.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<EventInfo> Events => Set<EventInfo>();
    public DbSet<EventSection> Sections => Set<EventSection>();
    public DbSet<Seat> Seats => Set<Seat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventInfo>(e =>
        {
            e.ToTable("events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Venue).HasMaxLength(200);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.Country).HasMaxLength(100);
            e.HasMany(x => x.Sections).WithOne().HasForeignKey(s => s.EventId);
        });

        modelBuilder.Entity<EventSection>(e =>
        {
            e.ToTable("event_sections");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.EventId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<Seat>(e =>
        {
            e.ToTable("seats");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            // Constraint clave para evitar venta doble: un asiento por sección/fila/número.
            e.HasIndex(x => new { x.SectionId, x.Row, x.SeatNumber }).IsUnique();
            // Optimistic concurrency: si dos transacciones intentan reservar el mismo asiento
            // a la vez, una sola gana. La otra recibe DbUpdateConcurrencyException.
            e.Property<uint>("xmin").IsRowVersion();
        });
    }
}
