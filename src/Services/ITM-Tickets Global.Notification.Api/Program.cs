using ITM_Tickets_Global.Notification.Api.Consumers;
using ITM_Tickets_Global.Notification.Api.Filters;
using ITM_Tickets_Global.Notification.Api.Hubs;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSignalR();
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.AddConsumer<OrderConfirmedConsumer>();
    x.AddConsumer<TicketReadyConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672");

        // Captura el correlation id que viene en los headers del mensaje y lo
        // propaga al scope del logger antes de ejecutar el consumer.
        cfg.UseConsumeFilter(typeof(CorrelationIdConsumeFilter<>), context);

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

app.UseCorrelationId();

// El hub debe matchear el path que el gateway forwardea: /api/notifications/...
app.MapHub<NotificationHub>("/api/notifications/hubs/notifications");

app.MapDefaultEndpoints();

app.Run();
