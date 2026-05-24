using ITM_Tickets_Global.Inventory.Api.Data;
using ITM_Tickets_Global.Inventory.Api.Interceptors;
using ITM_Tickets_Global.Inventory.Api.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Kestrel debe permitir HTTP/2 sobre texto plano (h2c) para gRPC sin TLS.
// IMPORTANTE: usar HttpProtocols.Http2 (NO Http1AndHttp2). Con Http1AndHttp2 sin TLS,
// Kestrel cae a HTTP/1.1 porque no puede negociar ALPN, y los clientes gRPC fallan
// con "HTTP/2 error code 'HTTP_1_1_REQUIRED'". Como este servicio es solo gRPC,
// forzar Http2 puro es lo correcto.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=itm_tickets;Username=itm_admin;Password=ChangeMe123!";

builder.Services.AddDbContext<InventoryDbContext>(opts => opts.UseNpgsql(connectionString));

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<CorrelationIdServerInterceptor>();
});
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.UseCorrelationId();

// Crea esquema y siembra inventario inicial si la BD está vacía.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    try
    {
        await db.Database.EnsureCreatedAsync();
        await InventorySeeder.SeedAsync(db);
        app.Logger.LogInformation("Inventory BD lista. Eventos sembrados: {Count}", db.Events.Count());
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Fallo creando/sembrando Inventory BD. El servicio arrancará igual y reintentará.");
    }
}

app.MapGrpcService<InventoryServiceImpl>();

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

app.MapGet("/", () => "ITM-Tickets Global Inventory.Api - gRPC Service");

app.MapDefaultEndpoints();

app.Run();
