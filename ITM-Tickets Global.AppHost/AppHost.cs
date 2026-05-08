using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);

var inventoryDb = postgres.AddDatabase("inventorydb");
var ordersDb = postgres.AddDatabase("ordersdb");

var inventoryApi = builder.AddProject<Projects.ITM_Tickets_Global_Inventory_Api>("inventory-api")
    .WithHttpHealthCheck("/health")
    .WithReference(inventoryDb)
    .WaitFor(postgres);

var orderApi = builder.AddProject<Projects.ITM_Tickets_Global_Order_Api>("order-api")
    .WithHttpHealthCheck("/health")
    .WithReference(rabbitMq)
    .WithReference(ordersDb)
    .WithReference(inventoryApi)
    .WaitFor(rabbitMq)
    .WaitFor(inventoryApi);

var priceApi = builder.AddProject<Projects.ITM_Tickets_Global_Price_Api>("price-api")
    .WithHttpHealthCheck("/health")
    .WithReference(redis)
    .WaitFor(redis);

var notificationApi = builder.AddProject<Projects.ITM_Tickets_Global_Notification_Api>("notification-api")
    .WithHttpHealthCheck("/health")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);

var searchApi = builder.AddProject<Projects.ITM_Tickets_Global_Search_Api>("search-api")
    .WithHttpHealthCheck("/health");

var authApi = builder.AddProject<Projects.ITM_Tickets_Global_ApiService>("auth-api")
    .WithHttpHealthCheck("/health");

var apiGateway = builder.AddProject<Projects.ITM_Tickets_Global_ApiGateway>("api-gateway")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(orderApi)
    .WithReference(priceApi)
    .WithReference(searchApi)
    .WithReference(notificationApi)
    .WithReference(authApi)
    .WaitFor(orderApi)
    .WaitFor(priceApi)
    .WaitFor(authApi);

builder.AddProject<Projects.ITM_Tickets_Global_Web>("web-frontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiGateway)
    .WaitFor(apiGateway);

builder.Build().Run();
