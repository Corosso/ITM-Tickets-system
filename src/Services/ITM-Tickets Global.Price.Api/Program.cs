using ITM_Tickets_Global.Price.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "ITMTickets_Price_";
});

builder.Services.AddSingleton<PriceService>();

var app = builder.Build();

app.UseCorrelationId();

app.MapGet("/api/prices/{eventId:guid}", async (Guid eventId, PriceService priceService) =>
{
    var prices = await priceService.GetPricesAsync(eventId);
    return prices.Count > 0 ? Results.Ok(prices) : Results.NotFound();
});

app.MapGet("/api/prices/{eventId:guid}/section/{section}", async (Guid eventId, string section, PriceService priceService) =>
{
    var price = await priceService.GetSectionPriceAsync(eventId, section);
    return price is not null ? Results.Ok(price) : Results.NotFound();
});

app.MapPost("/api/prices/refresh", async (PriceService priceService) =>
{
    await priceService.RefreshCacheAsync();
    return Results.Ok(new { Message = "Price cache refreshed" });
});

app.MapDefaultEndpoints();

app.Run();
