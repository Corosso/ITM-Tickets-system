using ITM_Tickets_Global.Search.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<SearchService>();

var app = builder.Build();

app.MapGet("/api/search", async (string q, string? vibe, SearchService searchService) =>
{
    var results = await searchService.SearchAsync(q, vibe);
    return Results.Ok(results);
});

app.MapDefaultEndpoints();

app.Run();
