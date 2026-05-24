using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using ITM_Tickets_Global.Search.Api.Models;
using Qdrant.Client;
// Alias para evitar colisión entre Shared.Dtos.SearchResponse y Qdrant.Client.Grpc.SearchResponse
using SearchResponse = ITM_Tickets_Global.Shared.Dtos.SearchResponse;

namespace ITM_Tickets_Global.Search.Api.Services;

/// <summary>
/// Búsqueda híbrida: combina match textual de Elasticsearch con búsqueda
/// semántica de Qdrant. Si el usuario provee `vibe`, los resultados se
/// reordenan combinando el score textual con la similitud coseno del vector.
/// </summary>
public class SearchService
{
    public const string ElasticIndex = "events";
    public const string QdrantCollection = "events_vec";

    private readonly ElasticsearchClient _es;
    private readonly QdrantClient _qdrant;
    private readonly EmbeddingService _embeddings;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        ElasticsearchClient es,
        QdrantClient qdrant,
        EmbeddingService embeddings,
        ILogger<SearchService> logger)
    {
        _es = es;
        _qdrant = qdrant;
        _embeddings = embeddings;
        _logger = logger;
    }

    public async Task<List<SearchResponse>> SearchAsync(string query, string? vibe = null)
    {
        _logger.LogInformation("Buscando '{Query}' (vibe='{Vibe}')", query, vibe ?? "-");

        // 1) Búsqueda textual en Elasticsearch.
        var multiMatch = new MultiMatchQuery
        {
            Query = query ?? string.Empty,
            Fields = Fields.FromStrings(new[] { "name^3", "description", "venue", "city", "tags" }),
            Fuzziness = new Fuzziness("AUTO")
        };

        var esRequest = new SearchRequest<EventDocument>(ElasticIndex)
        {
            Size = 20,
            Query = multiMatch
        };

        var esResp = await _es.SearchAsync<EventDocument>(esRequest);

        var textualHits = esResp.IsValidResponse
            ? esResp.Hits.Where(h => h.Source != null)
                .Select(h => (Doc: h.Source!, Score: (double)(h.Score ?? 0))).ToList()
            : new List<(EventDocument Doc, double Score)>();

        // 2) Si hay vibe, búsqueda semántica en Qdrant.
        var semanticBoost = new Dictionary<Guid, double>();
        if (!string.IsNullOrWhiteSpace(vibe))
        {
            try
            {
                var vector = _embeddings.Embed(vibe);
                var qResp = await _qdrant.SearchAsync(
                    collectionName: QdrantCollection,
                    vector: vector,
                    limit: 10);

                foreach (var point in qResp)
                {
                    if (point.Payload.TryGetValue("event_id", out var idVal)
                        && Guid.TryParse(idVal.StringValue, out var id))
                    {
                        semanticBoost[id] = point.Score;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Qdrant no respondió, devolvemos solo resultados textuales");
            }
        }

        // 3) Fusión: ordenamos por (score textual) + (semantic boost * 2).
        var merged = textualHits
            .Select(h => new
            {
                h.Doc,
                Score = h.Score + (semanticBoost.GetValueOrDefault(h.Doc.Id, 0) * 2)
            })
            .OrderByDescending(x => x.Score)
            .Take(10)
            .ToList();

        return merged.Select(m => new SearchResponse(
            m.Doc.Id, m.Doc.Name, m.Doc.Description,
            m.Doc.Venue, m.Doc.City, m.Doc.StartDate, m.Score
        )).ToList();
    }
}
