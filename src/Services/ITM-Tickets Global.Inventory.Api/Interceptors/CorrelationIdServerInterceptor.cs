using Grpc.Core;
using Grpc.Core.Interceptors;
using ITM_Tickets_Global.ServiceDefaults.CorrelationId;

namespace ITM_Tickets_Global.Inventory.Api.Interceptors;

/// <summary>
/// Lee el header x-correlation-id de las llamadas gRPC entrantes y lo
/// publica en CorrelationIdContext + en el scope del logger. Esto permite
/// que cuando Order.Api invoque ReserveSeats por gRPC, los logs de
/// Inventory.Api compartan el mismo Correlation ID.
/// </summary>
public sealed class CorrelationIdServerInterceptor : Interceptor
{
    private readonly ILogger<CorrelationIdServerInterceptor> _logger;

    public CorrelationIdServerInterceptor(ILogger<CorrelationIdServerInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = ExtractOrGenerate(context);
        CorrelationIdContext.Current = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdContext.LogScopeKey] = correlationId
        }))
        {
            // Formato [CID=xxx] consistente con el middleware HTTP de los demás
            // servicios, así un único `Select-String "<uuid>"` funciona en todos.
            _logger.LogInformation(
                "[CID={CorrelationId}] gRPC {Method}",
                correlationId, context.Method);
            // Eco del correlation id en los trailers para clientes interesados.
            context.ResponseTrailers.Add(CorrelationIdContext.GrpcMetadataKey, correlationId);
            return await continuation(request, context);
        }
    }

    private static string ExtractOrGenerate(ServerCallContext context)
    {
        foreach (var entry in context.RequestHeaders)
        {
            if (string.Equals(entry.Key, CorrelationIdContext.GrpcMetadataKey, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(entry.Value))
                {
                    return entry.Value;
                }
            }
        }
        return Guid.NewGuid().ToString();
    }
}
