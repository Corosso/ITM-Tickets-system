using ITM_Tickets_Global.Order.Api.Data;
using ITM_Tickets_Global.Order.Api.Filters;
using ITM_Tickets_Global.Order.Api.Interceptors;
using ITM_Tickets_Global.Order.Api.Sagas;
using ITM_Tickets_Global.Order.Api.Services;
using ITM_Tickets_Global.Shared.Protos;
using MassTransit;
using Microsoft.EntityFrameworkCore;

// Permite gRPC sobre HTTP/2 sin TLS (h2c). En docker-compose/K8s los servicios
// se hablan en plaintext detrás del Ingress.
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=itm_tickets_orders;Username=itm_admin;Password=ChangeMe123!";

builder.Services.AddDbContext<OrderDbContext>(opts => opts.UseNpgsql(connectionString));

builder.Services.AddTransient<CorrelationIdClientInterceptor>();

// Address directo al contenedor (Docker DNS resuelve "inventory-api" en la red itm-net).
// Evitamos el scheme mágico "https+http://" de Aspire service discovery porque acá
// no estamos corriendo bajo Aspire AppHost y la resolución es inconsistente.
builder.Services.AddGrpcClient<InventoryService.InventoryServiceClient>(o =>
{
    o.Address = new Uri(
        builder.Configuration["GrpcServices:Inventory"] ?? "http://inventory-api:8080");
}).AddInterceptor<CorrelationIdClientInterceptor>();

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.AddConsumer<ITM_Tickets_Global.Order.Api.Consumers.InventoryRequestConsumer>();

    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = MassTransit.ConcurrencyMode.Pessimistic;
            r.ExistingDbContext<OrderDbContext>();
            r.UsePostgres();
        });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672");

        // El correlation id viaja por RabbitMQ a través de los headers del mensaje.
        // Consume = restaura el CID en CorrelationIdContext (AsyncLocal) ANTES de
        //           que corra el consumer / saga. Sin esto, los hops async pierden
        //           el CID y todo lo aguas abajo genera GUIDs nuevos.
        // Publish/Send = inyectan el CID actual como header en los mensajes salientes.
        cfg.UseConsumeFilter(typeof(CorrelationIdConsumeFilter<>), context);
        cfg.UsePublishFilter(typeof(CorrelationIdPublishFilter<>), context);
        cfg.UseSendFilter(typeof(CorrelationIdSendFilter<>), context);

        // RESILIENCIA: si un consumer (ej. InventoryRequestConsumer llamando gRPC
        // a inventory-api) falla porque el downstream está caído, MassTransit
        // reintenta in-memory con backoff exponencial.
        // Tiempos: 5s, 15s, 25s, 35s, 45s, 60s → total ~3 minutos de gracia.
        // Si después de eso sigue fallando, el mensaje va a la cola _error.
        cfg.UseMessageRetry(r => r.Exponential(
            retryLimit: 6,
            minInterval: TimeSpan.FromSeconds(5),
            maxInterval: TimeSpan.FromSeconds(60),
            intervalDelta: TimeSpan.FromSeconds(10)));

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddScoped<OrderService>();

var app = builder.Build();

app.UseCorrelationId();

// Crea esquema del Saga + tabla de órdenes.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    try
    {
        await db.Database.EnsureCreatedAsync();
        app.Logger.LogInformation("Order BD lista. Saga persistido en PostgreSQL.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Fallo creando Order BD.");
    }
}

app.MapPost("/api/orders", async (CreateOrderRequest request, OrderService orderService) =>
{
    var result = await orderService.CreateOrderAsync(request);
    return result.Success
        ? Results.Ok(new { result.OrderId, Status = "Processing", Message = "Order created, awaiting inventory confirmation" })
        : Results.BadRequest(new { Message = result.Error });
});

app.MapGet("/api/orders/{orderId:guid}", async (Guid orderId, OrderService orderService) =>
{
    var status = await orderService.GetOrderStatusAsync(orderId);
    return status is not null ? Results.Ok(status) : Results.NotFound();
});

app.MapDefaultEndpoints();

app.Run();
