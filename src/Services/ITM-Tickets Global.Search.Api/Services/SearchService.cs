using ITM_Tickets_Global.Shared.Dtos;

namespace ITM_Tickets_Global.Search.Api.Services;

public class SearchService
{
    private readonly ILogger<SearchService> _logger;

    public SearchService(ILogger<SearchService> logger)
    {
        _logger = logger;
    }

    public async Task<List<SearchResponse>> SearchAsync(string query, string? vibe = null)
    {
        _logger.LogInformation("Searching for '{Query}' with vibe '{Vibe}'", query, vibe ?? "none");

        await Task.Delay(100);

        var results = new List<SearchResponse>
        {
            new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Festival de los Dos Mundos - Noche Inaugural",
                "Espectáculo de apertura con artistas de Colombia y España", "Teatro Metropolitano", "Medellín",
                DateTime.UtcNow.AddDays(30), 0.95),
            new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Festival de los Dos Mundos - Jazz Fusión",
                "Encuentro de jazz con músicos internacionales", "Auditorio Nacional", "Madrid",
                DateTime.UtcNow.AddDays(32), 0.85),
            new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Festival de los Dos Mundos - Danza Contemporánea",
                "Fusión de flamenco y danza colombiana", "Teatro Metropolitano", "Medellín",
                DateTime.UtcNow.AddDays(33), 0.78)
        };

        if (!string.IsNullOrEmpty(vibe))
        {
            results = results.Where(r =>
                r.Name.Contains(vibe, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(vibe, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        return results;
    }
}
