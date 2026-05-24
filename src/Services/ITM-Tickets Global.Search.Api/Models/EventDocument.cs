namespace ITM_Tickets_Global.Search.Api.Models;

/// <summary>
/// Documento indexado en Elasticsearch (búsqueda textual) y vectorizado en
/// Qdrant (búsqueda semántica). El Id es el mismo en ambos sistemas para
/// permitir join.
/// </summary>
public class EventDocument
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public DateTime StartDate { get; set; }
    public double BasePrice { get; set; }
}
