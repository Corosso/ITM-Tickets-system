using Grpc.Core;
using ITM_Tickets_Global.Shared.Protos;

namespace ITM_Tickets_Global.Inventory.Api.Services;

public class InventoryServiceImpl : InventoryService.InventoryServiceBase
{
    private readonly ILogger<InventoryServiceImpl> _logger;

    public InventoryServiceImpl(ILogger<InventoryServiceImpl> logger)
    {
        _logger = logger;
    }

    public override async Task<ReserveSeatsResponse> ReserveSeats(ReserveSeatsRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Reserving seats for Order {OrderId}, Items: {Count}",
            request.OrderId, request.Seats.Count);

        var failedSeats = new List<string>();

        foreach (var seat in request.Seats)
        {
            if (Random.Shared.Next(100) < 5)
            {
                failedSeats.Add($"S{seat.Section}-R{seat.Row}-N{seat.SeatNumber}");
            }
        }

        if (failedSeats.Count > 0)
        {
            return new ReserveSeatsResponse
            {
                Success = false,
                Message = "Some seats are no longer available",
                FailedSeats = { failedSeats }
            };
        }

        var reservationId = Guid.NewGuid().ToString();

        _logger.LogInformation("Seats reserved successfully for Order {OrderId}, ReservationId: {ReservationId}",
            request.OrderId, reservationId);

        return new ReserveSeatsResponse
        {
            Success = true,
            Message = "Seats reserved successfully",
            ReservationId = reservationId
        };
    }

    public override Task<ReleaseSeatsResponse> ReleaseSeats(ReleaseSeatsRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Releasing seats for Order {OrderId}, ReservationId: {ReservationId}",
            request.OrderId, request.ReservationId);

        return Task.FromResult(new ReleaseSeatsResponse
        {
            Success = true,
            Message = "Seats released successfully"
        });
    }

    public override Task<GetInventoryResponse> GetInventory(GetInventoryRequest request, ServerCallContext context)
    {
        var sections = new List<SectionInventory>
        {
            new SectionInventory
            {
                Section = "VIP",
                TotalSeats = 500,
                AvailableSeats = 500 - Random.Shared.Next(0, 200),
                BasePrice = 250.00
            },
            new SectionInventory
            {
                Section = "General",
                TotalSeats = 3000,
                AvailableSeats = 3000 - Random.Shared.Next(0, 500),
                BasePrice = 80.00
            },
            new SectionInventory
            {
                Section = "Platea",
                TotalSeats = 1500,
                AvailableSeats = 1500 - Random.Shared.Next(0, 300),
                BasePrice = 120.00
            }
        };

        return Task.FromResult(new GetInventoryResponse
        {
            EventId = request.EventId,
            Sections = { sections }
        });
    }

    public override Task<CheckAvailabilityResponse> CheckAvailability(CheckAvailabilityRequest request, ServerCallContext context)
    {
        var unavailable = new List<UnavailableSeat>();

        foreach (var seat in request.Seats)
        {
            if (Random.Shared.Next(100) < 10)
            {
                unavailable.Add(new UnavailableSeat
                {
                    Section = seat.Section,
                    Row = seat.Row,
                    SeatNumber = seat.SeatNumber,
                    Reason = "Already reserved"
                });
            }
        }

        return Task.FromResult(new CheckAvailabilityResponse
        {
            Available = unavailable.Count == 0,
            UnavailableSeats = { unavailable }
        });
    }
}
