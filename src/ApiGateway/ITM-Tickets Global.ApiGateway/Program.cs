using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ITM-Tickets-Global",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ITM-Tickets-App",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForITMTicketsGlobal2024!"))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Default", policy =>
    {
        policy.RequireAuthenticatedUser();
    });

    options.AddPolicy("PublicOnly", policy =>
    {
        policy.RequireAssertion(_ => true);
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("fixed", config =>
    {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromSeconds(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 10;
    });
});

builder.Services.AddReverseProxy()
    .LoadFromMemory(GetRoutes(), GetClusters());

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Gateway", "ITM-Tickets-Global-ApiGateway");
    context.Response.Headers.Append("X-Response-Time", DateTime.UtcNow.ToString("O"));
    await next();
});

app.MapReverseProxy();

app.MapGet("/", () => Results.Ok(new
{
    Service = "ITM-Tickets Global ApiGateway",
    Version = "1.0.0",
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Routes = new[]
    {
        new { Path = "/api/auth/{**catch-all}", Target = "auth-api" },
        new { Path = "/api/orders/{**catch-all}", Target = "order-api" },
        new { Path = "/api/prices/{**catch-all}", Target = "price-api" },
        new { Path = "/api/search/{**catch-all}", Target = "search-api" },
        new { Path = "/api/notifications/{**catch-all}", Target = "notification-api" }
    }
}));

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

app.Run();

static RouteConfig[] GetRoutes() =>
[
    new RouteConfig
    {
        RouteId = "auth",
        ClusterId = "auth-cluster",
        Match = new RouteMatch { Path = "/api/auth/{**catch-all}" },
        AuthorizationPolicy = "PublicOnly"
    },
    new RouteConfig
    {
        RouteId = "orders",
        ClusterId = "orders-cluster",
        Match = new RouteMatch { Path = "/api/orders/{**catch-all}" },
        AuthorizationPolicy = "Default"
    },
    new RouteConfig
    {
        RouteId = "prices",
        ClusterId = "prices-cluster",
        Match = new RouteMatch { Path = "/api/prices/{**catch-all}" }
    },
    new RouteConfig
    {
        RouteId = "search",
        ClusterId = "search-cluster",
        Match = new RouteMatch { Path = "/api/search/{**catch-all}" }
    },
    new RouteConfig
    {
        RouteId = "notifications",
        ClusterId = "notifications-cluster",
        Match = new RouteMatch { Path = "/api/notifications/{**catch-all}" },
        AuthorizationPolicy = "Default"
    }
];

static ClusterConfig[] GetClusters() =>
[
    new ClusterConfig
    {
        ClusterId = "auth-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["auth-api"] = new() { Address = "https://auth-api" }
        }
    },
    new ClusterConfig
    {
        ClusterId = "orders-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["order-api"] = new() { Address = "https://order-api" }
        }
    },
    new ClusterConfig
    {
        ClusterId = "prices-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["price-api"] = new() { Address = "https://price-api" }
        }
    },
    new ClusterConfig
    {
        ClusterId = "search-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["search-api"] = new() { Address = "https://search-api" }
        }
    },
    new ClusterConfig
    {
        ClusterId = "notifications-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["notification-api"] = new() { Address = "https://notification-api" }
        }
    }
];
