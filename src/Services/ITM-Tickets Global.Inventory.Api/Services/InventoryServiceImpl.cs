using Grpc.Core;
using ITM_Tickets_Global.Inventory.Api.Data;
using ITM_Tickets_Global.Shared.Models;
using ITM_Tickets_Global.Shared.Protos;
using Microsoft.EntityFrameworkCore;

namespace ITM_Tickets_Global.Inventory.Api.Services;

/// <summary>
/// Implementación gRPC con persistencia real en PostgreSQL.
/// Las reservas se hacen dentro de una transacción Serializable para evitar
/// venta doble incluso bajo los 50.000 usuarios concurrentes del enunciado.
/// </summary>
public class InventoryServiceImpl : InventoryService.InventoryServiceBase
{
    private readonly InventoryDbContext _db;
    private readonly ILogger<InventoryServiceImpl> _logger;

    public InventoryServiceImpl(InventoryDbContext db, ILogger<InventoryServiceImpl> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<ReserveSeatsResponse> ReserveSeats(ReserveSeatsRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Reservando {Count} asientos para orden {OrderId}", request.Seats.Count, request.OrderId);

        var orderId = Guid.Parse(request.OrderId);
        var failed = new List<string>();

        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        try
        {
            foreach (var requested in request.Seats)
            {
                var seat = await FindSeatAsync(requested);

                if (seat is null)
                {
                    _logger.LogWarning("Asiento NO EXISTE: EventId={EventId} Section={Section} Row={Row} Seat={Seat}",
                        requested.EventId, requested.Section, requested.Row, requested.SeatNumber);
                    failed.Add($"S{requested.Section}-R{requested.Row}-N{requested.SeatNumber} (no existe)");
                    continue;
                }

                if (seat.Status != SeatStatus.Available)
                {
                    _logger.LogWarning("Asiento NO DISPONIBLE: Section={Section} Row={Row} Seat={Seat} Status={Status} ReservedBy={ReservedBy}",
                        requested.Section, requested.Row, requested.SeatNumber, seat.Status, seat.ReservedByOrderId);
                    failed.Add($"S{requested.Section}-R{requested.Row}-N{requested.SeatNumber} (ya reservado)");
                    continue;
                }

                seat.Status = SeatStatus.Reserved;
                seat.ReservedByOrderId = orderId;
                seat.ReservedUntil = DateTime.UtcNow.AddMinutes(15);
            }

            if (failed.Count > 0)
            {
                await tx.RollbackAsync();
                _logger.LogWarning("Reserva FALLIDA para orden {OrderId}. Asientos: {Failed}",
                    orderId, string.Join(", ", failed));
                return new ReserveSeatsResponse
                {
                    Success = false,
                    Message = "Algunos asientos ya no están disponibles: " + string.Join(", ", failed),
                    FailedSeats = { failed }
                };
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var reservationId = orderId.ToString();
            _logger.LogInformation("Reservados {Count} asientos. ReservationId={ReservationId}", request.Seats.Count, reservationId);

            return new ReserveSeatsResponse
            {
                Success = true,
                Message = "Asientos reservados",
                ReservationId = reservationId
            };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync();
            _logger.LogWarning(ex, "Conflicto de concurrencia reservando asientos para {OrderId}", orderId);
            return new ReserveSeatsResponse
            {
                Success = false,
                Message = "Conflicto de concurrencia: otra orden tomó algunos asientos al mismo tiempo"
            };
        }
    }

    public override async Task<ReleaseSeatsResponse> ReleaseSeats(ReleaseSeatsRequest request, ServerCallContext context)
    {
        var orderId = Guid.Parse(request.OrderId);
        var reserved = await _db.Seats.Where(s => s.ReservedByOrderId == orderId).ToListAsync();

        foreach (var s in reserved)
        {
            s.Status = SeatStatus.Available;
            s.ReservedByOrderId = null;
            s.ReservedUntil = null;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Liberados {Count} asientos para orden {OrderId}", reserved.Count, orderId);

        return new ReleaseSeatsResponse { Success = true, Message = "Asientos liberados" };
    }

    public override async Task<GetInventoryResponse> GetInventory(GetInventoryRequest request, ServerCallContext context)
    {
        var eventId = Guid.Parse(request.EventId);

        var sections = await _db.Sections.Where(s => s.EventId == eventId).ToListAsync();

        var result = new GetInventoryResponse { EventId = request.EventId };
        foreach (var sec in sections)
        {
            var available = await _db.Seats.CountAsync(s => s.SectionId == sec.Id && s.Status == SeatStatus.Available);
            result.Sections.Add(new SectionInventory
            {
                Section = sec.Name,
                TotalSeats = sec.TotalSeats,
                AvailableSeats = available,
                BasePrice = sec.BasePrice
            });
        }
        return result;
    }

    public override async Task<CheckAvailabilityResponse> CheckAvailability(CheckAvailabilityRequest request, ServerCallContext context)
    {
        var unavailable = new List<UnavailableSeat>();

        foreach (var requested in request.Seats)
        {
            var seat = await FindSeatAsync(requested);
            if (seat is null || seat.Status != SeatStatus.Available)
            {
                unavailable.Add(new UnavailableSeat
                {
                    Section = requested.Section,
                    Row = requested.Row,
                    SeatNumber = requested.SeatNumber,
                    Reason = seat is null ? "No existe" : "Ya reservado"
                });
            }
        }

        return new CheckAvailabilityResponse
        {
            Available = unavailable.Count == 0,
            UnavailableSeats = { unavailable }
        };
    }

    /// <summary>
    /// Busca un asiento por (eventId, section, row, seatNumber). El join se
    /// hace contra Sections para resolver el SectionId a partir del nombre
    /// dentro del evento correcto (recordar que "VIP" existe para Medellín y Madrid).
    /// </summary>
    private async Task<Seat?> FindSeatAsync(ITM_Tickets_Global.Shared.Protos.SeatRequest requested)
    {
        Guid.TryParse(requested.EventId, out var eventId);

        return await (
            from s in _db.Seats
            join sec in _db.Sections on s.SectionId equals sec.Id
            where (eventId == Guid.Empty || sec.EventId == eventId)
               && sec.Name == requested.Section
               && s.Row == requested.Row
               && s.SeatNumber == requested.SeatNumber
            select s
        ).FirstOrDefaultAsync();
    }
}
