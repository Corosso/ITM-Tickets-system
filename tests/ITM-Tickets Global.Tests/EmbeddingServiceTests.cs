using ITM_Tickets_Global.Search.Api.Services;
using Xunit;

namespace ITM_Tickets_Global.Tests;

public class EmbeddingServiceTests
{
    private readonly EmbeddingService _service = new();

    [Fact]
    public void Embed_DimensionEsLaEsperada()
    {
        var v = _service.Embed("festival de los dos mundos");
        Assert.Equal(EmbeddingService.Dimensions, v.Length);
    }

    [Fact]
    public void Embed_VectorEstaNormalizadoL2()
    {
        var v = _service.Embed("flamenco fusión jazz");
        var norm = Math.Sqrt(v.Sum(x => x * x));
        Assert.InRange(norm, 0.99, 1.01);
    }

    [Fact]
    public void Embed_TextosSimilaresProducenVectoresParecidos()
    {
        var a = _service.Embed("noche de jazz fusion");
        var b = _service.Embed("noche de jazz");
        var c = _service.Embed("electrofiesta rave");

        double Cosine(float[] x, float[] y) =>
            x.Zip(y, (xi, yi) => (double)(xi * yi)).Sum();

        var simAB = Cosine(a, b);
        var simAC = Cosine(a, c);

        Assert.True(simAB > simAC, $"Esperaba sim(jazz,jazz)={simAB} > sim(jazz,electro)={simAC}");
    }

    [Fact]
    public void Embed_TextoVacioRetornaVectorCero()
    {
        var v = _service.Embed("");
        Assert.All(v, x => Assert.Equal(0f, x));
    }
}
