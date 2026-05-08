using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ITM_Tickets_Global.Price.Api.Services;

public class PriceService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<PriceService> _logger;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public PriceService(IDistributedCache cache, ILogger<PriceService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<PriceResponse>> GetPricesAsync(Guid eventId)
    {
        var cacheKey = $"prices:{eventId}";

        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached is not null)
        {
            _logger.LogInformation("Cache HIT for event {EventId}", eventId);
            return JsonSerializer.Deserialize<List<PriceResponse>>(cached) ?? [];
        }

        _logger.LogInformation("Cache MISS for event {EventId}, computing prices", eventId);

        var prices = GeneratePrices(eventId);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheExpiration
        };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(prices), options);

        return prices;
    }

    public async Task<PriceResponse?> GetSectionPriceAsync(Guid eventId, string section)
    {
        var prices = await GetPricesAsync(eventId);
        return prices.FirstOrDefault(p => p.Section.Equals(section, StringComparison.OrdinalIgnoreCase));
    }

    public async Task RefreshCacheAsync()
    {
        _logger.LogInformation("Refreshing price cache...");
        await Task.CompletedTask;
    }

    private static List<PriceResponse> GeneratePrices(Guid eventId)
    {
        return
        [
            new PriceResponse(eventId, "VIP", 250.00, 200.00, 1.25, DateTime.UtcNow),
            new PriceResponse(eventId, "General", 80.00, 80.00, 1.00, DateTime.UtcNow),
            new PriceResponse(eventId, "Platea", 120.00, 100.00, 1.20, DateTime.UtcNow),
            new PriceResponse(eventId, "Palco", 350.00, 300.00, 1.17, DateTime.UtcNow)
        ];
    }
}

public record PriceResponse(
    Guid EventId,
    string Section,
    double CurrentPrice,
    double BasePrice,
    double Multiplier,
    DateTime LastUpdated
);
