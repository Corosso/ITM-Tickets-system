using Grpc.Core;
using Grpc.Core.Interceptors;
using ITM_Tickets_Global.ServiceDefaults.CorrelationId;

namespace ITM_Tickets_Global.Order.Api.Interceptors;

/// <summary>
/// Adjunta el header x-correlation-id a TODA llamada gRPC saliente. Esto
/// garantiza que cuando Order.Api invoque Inventory.Api por gRPC, el
/// correlation id viaje en los metadatos y aparezca en los logs de ambos
/// servicios.
/// </summary>
public sealed class CorrelationIdClientInterceptor : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var newContext = WithCorrelation(context);
        return continuation(request, newContext);
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var newContext = WithCorrelation(context);
        return continuation(request, newContext);
    }

    private static ClientInterceptorContext<TReq, TResp> WithCorrelation<TReq, TResp>(
        ClientInterceptorContext<TReq, TResp> context)
        where TReq : class where TResp : class
    {
        var metadata = context.Options.Headers ?? new Metadata();
        if (!metadata.Any(m => string.Equals(m.Key, CorrelationIdContext.GrpcMetadataKey, StringComparison.OrdinalIgnoreCase)))
        {
            metadata.Add(CorrelationIdContext.GrpcMetadataKey, CorrelationIdContext.GetOrCreate());
        }
        var newOptions = context.Options.WithHeaders(metadata);
        return new ClientInterceptorContext<TReq, TResp>(context.Method, context.Host, newOptions);
    }
}
