using ITM_Tickets_Global.Shared.Models;

namespace ITM_Tickets_Global.Inventory.Api.Data;

/// <summary>
/// Carga datos iniciales del Festival de los Dos Mundos. Siembra EXACTAMENTE
/// los mismos eventos que el SearchSeeder del search-api, así todo evento que
/// aparezca en la búsqueda es comprable (sin "eventos fantasma" sin asientos).
/// Solo siembra si la BD está vacía: idempotente para reinicios de Pod.
/// </summary>
public static class InventorySeeder
{
    public static readonly Guid MedellinEventId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    public static readonly Guid MadridEventId   = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    public static readonly Guid JazzEventId     = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
    public static readonly Guid DanzaEventId    = Guid.Parse("dddddddd-4444-4444-4444-444444444444");
    public static readonly Guid ElectroEventId  = Guid.Parse("eeeeeeee-5555-5555-5555-555555555555");

    public static async Task SeedAsync(InventoryDbContext db, CancellationToken ct = default)
    {
        if (db.Events.Any()) return;

        // Los IDs y nombres DEBEN coincidir con SearchSeeder.DemoEvents.
        var events = new[]
        {
            new EventInfo
            {
                Id = MedellinEventId,
                Name = "Festival de los Dos Mundos - Sede Medellín",
                Description = "Apertura del World Tour 2026.",
                StartDate = DateTime.UtcNow.AddDays(30),
                EndDate = DateTime.UtcNow.AddDays(33),
                Venue = "Teatro Metropolitano",
                City = "Medellín",
                Country = "Colombia",
                IsActive = true,
            },
            new EventInfo
            {
                Id = MadridEventId,
                Name = "Festival de los Dos Mundos - Sede Madrid",
                Description = "Cierre simultáneo del festival.",
                StartDate = DateTime.UtcNow.AddDays(30),
                EndDate = DateTime.UtcNow.AddDays(33),
                Venue = "Teatro Real",
                City = "Madrid",
                Country = "España",
                IsActive = true,
            },
            new EventInfo
            {
                Id = JazzEventId,
                Name = "Noche de Jazz Latino",
                Description = "Encuentro íntimo de jazz fusion.",
                StartDate = DateTime.UtcNow.AddDays(32),
                EndDate = DateTime.UtcNow.AddDays(32),
                Venue = "Auditorio Nacional",
                City = "Madrid",
                Country = "España",
                IsActive = true,
            },
            new EventInfo
            {
                Id = DanzaEventId,
                Name = "Danza Contemporánea Bicontinental",
                Description = "Fusión de flamenco y danza contemporánea colombiana.",
                StartDate = DateTime.UtcNow.AddDays(33),
                EndDate = DateTime.UtcNow.AddDays(33),
                Venue = "Teatro Metropolitano",
                City = "Medellín",
                Country = "Colombia",
                IsActive = true,
            },
            new EventInfo
            {
                Id = ElectroEventId,
                Name = "Electrofiesta - DJs del Mundo",
                Description = "Festival electrónico nocturno con DJs internacionales.",
                StartDate = DateTime.UtcNow.AddDays(35),
                EndDate = DateTime.UtcNow.AddDays(35),
                Venue = "Recinto Ferial",
                City = "Madrid",
                Country = "España",
                IsActive = true,
            },
        };

        db.Events.AddRange(events);
        await db.SaveChangesAsync(ct);

        foreach (var ev in events)
        {
            // Cada evento: VIP, Platea, General (capacidad reducida respecto a un estadio real, suficiente para pruebas locales).
            var sections = new[]
            {
                new EventSection { Id = Guid.NewGuid(), EventId = ev.Id, Name = "VIP",     TotalSeats = 50,  AvailableSeats = 50,  BasePrice = 250 },
                new EventSection { Id = Guid.NewGuid(), EventId = ev.Id, Name = "Platea",  TotalSeats = 150, AvailableSeats = 150, BasePrice = 120 },
                new EventSection { Id = Guid.NewGuid(), EventId = ev.Id, Name = "General", TotalSeats = 300, AvailableSeats = 300, BasePrice = 80  },
            };
            db.Sections.AddRange(sections);
            await db.SaveChangesAsync(ct);

            foreach (var sec in sections)
            {
                var seatsPerRow = sec.Name switch { "VIP" => 10, "Platea" => 15, _ => 20 };
                var rows = sec.TotalSeats / seatsPerRow;
                for (var row = 1; row <= rows; row++)
                {
                    for (var num = 1; num <= seatsPerRow; num++)
                    {
                        db.Seats.Add(new Seat
                        {
                            Id = Guid.NewGuid(),
                            SectionId = sec.Id,
                            Row = row,
                            SeatNumber = num,
                            Status = SeatStatus.Available
                        });
                    }
                }
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
