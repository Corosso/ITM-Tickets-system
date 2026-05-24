using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ITM_Tickets_Global.MauiApp.Services;

/// <summary>
/// Cliente HTTP que llama al API Gateway (YARP). Inyecta automáticamente
/// el JWT y propaga el header X-Correlation-Id para trazabilidad.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public ApiClient(HttpClient http, TokenStore tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public async Task<AuthResponse?> LoginAsync(string username, string password)
    {
        using var req = CreateRequest(HttpMethod.Post, "/api/auth/login");
        req.Content = JsonContent.Create(new { Username = username, Password = password });

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;

        return await resp.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task<List<EventDto>> SearchAsync(string query, string? vibe = null)
    {
        var url = $"/api/search?q={Uri.EscapeDataString(query)}";
        if (!string.IsNullOrWhiteSpace(vibe)) url += $"&vibe={Uri.EscapeDataString(vibe)}";

        using var req = CreateRequest(HttpMethod.Get, url);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return new();

        return await resp.Content.ReadFromJsonAsync<List<EventDto>>() ?? new();
    }

    public async Task<CreateOrderResult?> CreateOrderAsync(CreateOrderDto order, string correlationId)
    {
        using var req = CreateRequest(HttpMethod.Post, "/api/orders", correlationId);
        req.Content = JsonContent.Create(order);

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;

        return await resp.Content.ReadFromJsonAsync<CreateOrderResult>();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? correlationId = null)
    {
        var req = new HttpRequestMessage(method, path);

        if (!string.IsNullOrEmpty(_tokens.Token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.Token);
        }

        // Correlation id de cliente: viaja al gateway y de ahí a cada microservicio.
        // Si el llamador necesita un ID conocido para rastrearlo en los logs
        // (ej. CreateOrder, que lo muestra en la UI), se reusa el provisto. Si
        // no se pasa ninguno, se genera uno nuevo automáticamente.
        req.Headers.TryAddWithoutValidation(
            "X-Correlation-Id",
            correlationId ?? Guid.NewGuid().ToString());

        return req;
    }
}

public record AuthResponse(string Token, DateTime ExpiresAt, string Username);

public record EventDto(Guid Id, string Name, string Description, string Venue, string City, DateTime StartDate, double Score);

public record CreateOrderDto(Guid UserId, string Email, string PhoneNumber, List<OrderItemDto> Items);

public record OrderItemDto(Guid EventId, string Section, int Row, int SeatNumber, int Quantity, double UnitPrice);

public record CreateOrderResult(Guid OrderId, string Status, string Message);
