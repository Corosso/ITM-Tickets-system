using ITM_Tickets_Global.ServiceDefaults.CorrelationId;
using MassTransit;

namespace ITM_Tickets_Global.Order.Api.Filters;

/// <summary>
/// Filtro de MassTransit que inyecta el Correlation ID actual como header
/// del mensaje publicado. Se aplica tanto a Publish() como a Send().
/// </summary>
public sealed class CorrelationIdPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        var correlationId = CorrelationIdContext.GetOrCreate();
        context.Headers.Set(CorrelationIdContext.HeaderName, correlationId);
        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("correlation-id-publish");
}

public sealed class CorrelationIdSendFilter<T> : IFilter<SendContext<T>> where T : class
{
    public Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        var correlationId = CorrelationIdContext.GetOrCreate();
        context.Headers.Set(CorrelationIdContext.HeaderName, correlationId);
        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("correlation-id-send");
}
