namespace ITM_Tickets_Global.Shared.Models;

public class EventInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Venue { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<EventSection> Sections { get; set; } = [];
}

public class EventSection
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public double BasePrice { get; set; }
}

public class Seat
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public int Row { get; set; }
    public int SeatNumber { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Available;
    public Guid? ReservedByOrderId { get; set; }
    public DateTime? ReservedUntil { get; set; }
}

public enum SeatStatus
{
    Available = 0,
    Reserved = 1,
    Sold = 2,
    Blocked = 3
}

public class PriceInfo
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Section { get; set; } = string.Empty;
    public double BasePrice { get; set; }
    public double DynamicMultiplier { get; set; } = 1.0;
    public double CurrentPrice => Math.Round(BasePrice * DynamicMultiplier, 2);
    public DateTime LastUpdated { get; set; }
}
