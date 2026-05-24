using ITM_Tickets_Global.ServiceDefaults.CorrelationId;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ITM_Tickets_Global.Order.Api.Filters;

/// <summary>
/// Filtro de MassTransit que extrae X-Correlation-Id del header del mensaje
/// entrante y lo publica en CorrelationIdContext (AsyncLocal) + en el scope
/// del logger ANTES de que corra el consumer / saga.
///
/// Crítico para que el Correlation ID sobreviva los saltos asíncronos por
/// RabbitMQ: si no se restaura acá, los consumers de Order.Api operan con
/// CorrelationIdContext.Current = null, y todos los hops siguientes
/// (gRPC a inventory-api, publicación de OrderConfirmed, etc.) generan
/// GUIDs nuevos en vez de heredar el CID original.
/// </summary>
public sealed class CorrelationIdConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly ILogger<CorrelationIdConsumeFilter<T>> _logger;

    public CorrelationIdConsumeFilter(ILogger<CorrelationIdConsumeFilter<T>> logger)
    {
        _logger = logger;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var correlationId = context.Headers.Get<string>(CorrelationIdContext.HeaderName)
                            ?? context.CorrelationId?.ToString()
                            ?? Guid.NewGuid().ToString();

        CorrelationIdContext.Current = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdContext.LogScopeKey] = correlationId
        }))
        {
            // Log explícito con [CID=xxx] en el mensaje, igual que en el
            // middleware HTTP, para que `docker logs order-api | Select-String`
            // encuentre el correlation id también en las invocaciones que
            // entran por RabbitMQ (no solo las que entran por HTTP).
            _logger.LogInformation(
                "[CID={CorrelationId}] Consumiendo {MessageType}",
                correlationId, typeof(T).Name);
            await next.Send(context);
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("correlation-id-consume");
}
