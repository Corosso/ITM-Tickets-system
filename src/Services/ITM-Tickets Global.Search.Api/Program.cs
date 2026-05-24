using Elastic.Clients.Elasticsearch;
using ITM_Tickets_Global.Search.Api.Services;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ---- Elasticsearch ----
var esUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
var esSettings = new ElasticsearchClientSettings(new Uri(esUri))
    .DefaultIndex(SearchService.ElasticIndex);
builder.Services.AddSingleton(new ElasticsearchClient(esSettings));

// ---- Qdrant ----
var qdrantHost = builder.Configuration["Qdrant:Host"] ?? "localhost";
var qdrantPort = int.Parse(builder.Configuration["Qdrant:Port"] ?? "6334");
builder.Services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort));

builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<SearchService>();

var app = builder.Build();

app.UseCorrelationId();

// Crear índice + colección + datos iniciales al arranque (idempotente).
_ = Task.Run(async () =>
{
    // Reintenta hasta que Elasticsearch y Qdrant estén listos.
    var attempts = 0;
    while (attempts < 30)
    {
        attempts++;
        try
        {
            using var scope = app.Services.CreateScope();
            var es = scope.ServiceProvider.GetRequiredService<ElasticsearchClient>();
            var qd = scope.ServiceProvider.GetRequiredService<QdrantClient>();
            var emb = scope.ServiceProvider.GetRequiredService<EmbeddingService>();
            await SearchSeeder.SeedAsync(es, qd, emb, app.Logger);
            app.Logger.LogInformation("Seed de Elasticsearch + Qdrant completado.");
            return;
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning("Seed pendiente (intento {Attempt}/30): {Reason}", attempts, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
    app.Logger.LogError("Seed de Search.Api FALLÓ tras 30 intentos. Revisar conectividad.");
});

app.MapGet("/api/search", async (string q, string? vibe, SearchService searchService) =>
{
    var results = await searchService.SearchAsync(q, vibe);
    return Results.Ok(results);
});

app.MapDefaultEndpoints();

app.Run();
