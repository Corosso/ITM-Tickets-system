using ITM_Tickets_Global.Order.Api.Services;
using ITM_Tickets_Global.Shared.Protos;
using MassTransit;
using ITM_Tickets_Global.Order.Api.Sagas;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddGrpcClient<InventoryService.InventoryServiceClient>(o =>
{
    o.Address = new Uri("https+http://inventory-api");
});

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .InMemoryRepository();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672");
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddSingleton<OrderService>();

var app = builder.Build();

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
