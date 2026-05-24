using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ITM_Tickets_Global.ServiceDefaults.CorrelationId;

/// <summary>
/// Middleware HTTP que:
///   1. Lee el header X-Correlation-Id de la petición entrante.
///   2. Si no existe, genera uno nuevo (Guid).
///   3. Lo publica en CorrelationIdContext (AsyncLocal) y en el scope del logger.
///   4. Lo refleja en la respuesta para que el cliente lo pueda ver.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        CorrelationIdContext.Current = correlationId;

        // También lo escribimos en el Request para que reverse-proxies como YARP
        // lo reenvíen al servicio aguas abajo.
        context.Request.Headers[CorrelationIdContext.HeaderName] = correlationId;
        context.Response.Headers[CorrelationIdContext.HeaderName] = correlationId;

        // Asegura que el correlation id aparezca en TODOS los logs emitidos
        // dentro del scope de la petición.
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdContext.LogScopeKey] = correlationId
        }))
        {
            // Log explícito al iniciar y terminar la petición. Esto garantiza
            // que el correlation id sea grep-eable en `docker logs` sin depender
            // de la configuración de IncludeScopes del console logger.
            _logger.LogInformation(
                "[CID={CorrelationId}] >> {Method} {Path}",
                correlationId, context.Request.Method, context.Request.Path);

            await _next(context);

            _logger.LogInformation(
                "[CID={CorrelationId}] << {Method} {Path} {StatusCode}",
                correlationId, context.Request.Method, context.Request.Path, context.Response.StatusCode);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdContext.HeaderName, out var values))
        {
            var existing = values.ToString();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }
        }
        return Guid.NewGuid().ToString();
    }
}
