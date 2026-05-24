namespace ITM_Tickets_Global.ServiceDefaults.CorrelationId;

/// <summary>
/// Contexto distribuido para el Correlation ID. Se propaga a través de toda la
/// cadena async sin necesidad de inyectarlo manualmente en cada método.
///
/// El Correlation ID viaja por:
///   - HTTP: header X-Correlation-Id (entrada y salida)
///   - gRPC: metadata x-correlation-id
///   - RabbitMQ / MassTransit: header X-Correlation-Id en el mensaje
///   - Logs: scope con la clave "CorrelationId"
/// </summary>
public static class CorrelationIdContext
{
    public const string HeaderName = "X-Correlation-Id";
    public const string GrpcMetadataKey = "x-correlation-id";
    public const string LogScopeKey = "CorrelationId";

    private static readonly AsyncLocal<string?> _current = new();

    /// <summary>
    /// Correlation ID actual en este flujo lógico. Null si todavía no se inicializó.
    /// </summary>
    public static string? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    /// <summary>
    /// Devuelve el Correlation ID actual o genera uno nuevo si todavía no existe.
    /// </summary>
    public static string GetOrCreate()
    {
        if (string.IsNullOrEmpty(_current.Value))
        {
            _current.Value = Guid.NewGuid().ToString();
        }
        return _current.Value!;
    }
}
