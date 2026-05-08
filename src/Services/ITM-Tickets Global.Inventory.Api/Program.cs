using ITM_Tickets_Global.Inventory.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.MapGrpcService<InventoryServiceImpl>();

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

app.MapGet("/", () => "ITM-Tickets Global Inventory.Api - gRPC Service");

app.MapDefaultEndpoints();

app.Run();
