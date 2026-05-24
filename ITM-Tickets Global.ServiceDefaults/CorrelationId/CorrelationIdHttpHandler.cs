namespace ITM_Tickets_Global.ServiceDefaults.CorrelationId;

/// <summary>
/// DelegatingHandler que adjunta el header X-Correlation-Id a todas las
/// llamadas HTTP salientes. Se engancha a través de
/// ConfigureHttpClientDefaults en AddServiceDefaults.
/// </summary>
public sealed class CorrelationIdHttpHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(CorrelationIdContext.HeaderName))
        {
            var correlationId = CorrelationIdContext.GetOrCreate();
            request.Headers.TryAddWithoutValidation(CorrelationIdContext.HeaderName, correlationId);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
