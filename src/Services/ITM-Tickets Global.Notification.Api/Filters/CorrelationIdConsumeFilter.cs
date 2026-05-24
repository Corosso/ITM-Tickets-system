using ITM_Tickets_Global.ServiceDefaults.CorrelationId;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ITM_Tickets_Global.Notification.Api.Filters;

/// <summary>
/// Filtro de MassTransit que extrae X-Correlation-Id de los headers del
/// mensaje entrante y lo publica en CorrelationIdContext + scope del logger.
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
            // Formato [CID=xxx] consistente con el middleware HTTP y con el resto
            // de los servicios para que un único Select-String "<uuid>" matchee
            // en order-api, inventory-api y notification-api.
            _logger.LogInformation(
                "[CID={CorrelationId}] Consumiendo {MessageType}",
                correlationId, typeof(T).Name);
            await next.Send(context);
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("correlation-id-consume");
}
