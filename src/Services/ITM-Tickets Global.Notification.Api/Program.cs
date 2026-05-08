using ITM_Tickets_Global.Notification.Api.Hubs;
using ITM_Tickets_Global.Notification.Api.Consumers;
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
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

app.MapHub<NotificationHub>("/hubs/notifications");

app.MapDefaultEndpoints();

app.Run();
