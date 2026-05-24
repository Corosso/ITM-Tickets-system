using ITM_Tickets_Global.ServiceDefaults.CorrelationId;
using Xunit;

namespace ITM_Tickets_Global.Tests;

public class CorrelationIdTests
{
    [Fact]
    public void GetOrCreate_GeneraGuidValidoCuandoNoExiste()
    {
        CorrelationIdContext.Current = null;
        var id = CorrelationIdContext.GetOrCreate();
        Assert.True(Guid.TryParse(id, out _));
    }

    [Fact]
    public void GetOrCreate_DevuelveElMismoIdEnLaMismaCadenaAsync()
    {
        CorrelationIdContext.Current = null;
        var id1 = CorrelationIdContext.GetOrCreate();
        var id2 = CorrelationIdContext.GetOrCreate();
        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task Context_SePropagaATravesDeAwait()
    {
        CorrelationIdContext.Current = "test-id-123";
        await Task.Delay(10);
        await Task.Run(() => { /* salto a otro hilo */ });
        // El AsyncLocal de la cadena original sigue intacto.
        Assert.Equal("test-id-123", CorrelationIdContext.Current);
    }
}
