using Elastic.Clients.Elasticsearch;
using ITM_Tickets_Global.Search.Api.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace ITM_Tickets_Global.Search.Api.Services;

/// <summary>
/// Crea el índice de Elasticsearch y la colección de Qdrant, e indexa los
/// eventos del Festival de los Dos Mundos en ambos sistemas. Es idempotente.
/// </summary>
public static class SearchSeeder
{
    public static readonly EventDocument[] DemoEvents =
    [
        new()
        {
            Id = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
            Name = "Festival de los Dos Mundos - Sede Medellín",
            Description = "Apertura del World Tour 2026. Mezcla de cumbia, salsa y rumba flamenca con artistas de Colombia y España.",
            Venue = "Teatro Metropolitano",
            City = "Medellín",
            Country = "Colombia",
            Tags = ["cumbia", "salsa", "flamenco", "festival", "musica latina", "fiesta", "energia"],
            StartDate = DateTime.UtcNow.AddDays(30),
            BasePrice = 80
        },
        new()
        {
            Id = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222"),
            Name = "Festival de los Dos Mundos - Sede Madrid",
            Description = "Cierre simultáneo en Madrid. Flamenco fusión con bandas colombianas. Noche de tablao y percusión.",
            Venue = "Teatro Real",
            City = "Madrid",
            Country = "España",
            Tags = ["flamenco", "tablao", "fusion", "tradicional", "intimo", "elegante"],
            StartDate = DateTime.UtcNow.AddDays(30),
            BasePrice = 120
        },
        new()
        {
            Id = Guid.Parse("cccccccc-3333-3333-3333-333333333333"),
            Name = "Noche de Jazz Latino",
            Description = "Encuentro íntimo de jazz fusion con músicos internacionales en formato club. Bebida incluida.",
            Venue = "Auditorio Nacional",
            City = "Madrid",
            Country = "España",
            Tags = ["jazz", "fusion", "intimo", "club", "tranquilo", "chill", "improvisacion"],
            StartDate = DateTime.UtcNow.AddDays(32),
            BasePrice = 90
        },
        new()
        {
            Id = Guid.Parse("dddddddd-4444-4444-4444-444444444444"),
            Name = "Danza Contemporánea Bicontinental",
            Description = "Fusión de flamenco y danza contemporánea colombiana. Espectáculo visual de impacto.",
            Venue = "Teatro Metropolitano",
            City = "Medellín",
            Country = "Colombia",
            Tags = ["danza", "contemporanea", "flamenco", "visual", "artistico", "elegante"],
            StartDate = DateTime.UtcNow.AddDays(33),
            BasePrice = 100
        },
        new()
        {
            Id = Guid.Parse("eeeeeeee-5555-5555-5555-555555555555"),
            Name = "Electrofiesta - DJs del Mundo",
            Description = "Festival electrónico nocturno con DJs internacionales. Música hasta el amanecer.",
            Venue = "Recinto Ferial",
            City = "Madrid",
            Country = "España",
            Tags = ["electronica", "fiesta", "rave", "energia", "noche", "djs"],
            StartDate = DateTime.UtcNow.AddDays(35),
            BasePrice = 60
        }
    ];

    public static async Task SeedAsync(
        ElasticsearchClient es,
        QdrantClient qdrant,
        EmbeddingService embeddings,
        ILogger logger,
        CancellationToken ct = default)
    {
        // ---- Elasticsearch ----
        var indexExists = await es.Indices.ExistsAsync(SearchService.ElasticIndex, ct);
        if (!indexExists.Exists)
        {
            // Mapping mínimo: confiamos en la inferencia automática para los demás campos.
            var create = await es.Indices.CreateAsync(SearchService.ElasticIndex, ct);
            logger.LogInformation("Índice Elasticsearch creado: {Index} (success={Success})",
                SearchService.ElasticIndex, create.IsValidResponse);
        }

        foreach (var doc in DemoEvents)
        {
            await es.IndexAsync(doc, idx => idx.Index(SearchService.ElasticIndex).Id(doc.Id.ToString()), ct);
        }
        await es.Indices.RefreshAsync(SearchService.ElasticIndex, ct);
        logger.LogInformation("Indexados {Count} eventos en Elasticsearch", DemoEvents.Length);

        // ---- Qdrant ----
        var collections = await qdrant.ListCollectionsAsync(ct);
        if (!collections.Contains(SearchService.QdrantCollection))
        {
            await qdrant.CreateCollectionAsync(
                SearchService.QdrantCollection,
                new VectorParams { Size = (ulong)EmbeddingService.Dimensions, Distance = Distance.Cosine },
                cancellationToken: ct);
            logger.LogInformation("Colección Qdrant creada: {Collection}", SearchService.QdrantCollection);
        }

        var points = new List<PointStruct>();
        foreach (var doc in DemoEvents)
        {
            var text = $"{doc.Name}. {doc.Description}. {string.Join(' ', doc.Tags)}";
            var vector = embeddings.Embed(text);
            var point = new PointStruct
            {
                Id = new PointId { Uuid = doc.Id.ToString() },
                Vectors = vector,
            };
            point.Payload.Add("event_id", new Value { StringValue = doc.Id.ToString() });
            point.Payload.Add("name", new Value { StringValue = doc.Name });
            points.Add(point);
        }

        await qdrant.UpsertAsync(SearchService.QdrantCollection, points, cancellationToken: ct);
        logger.LogInformation("Upsert {Count} vectores en Qdrant", points.Count);
    }
}
