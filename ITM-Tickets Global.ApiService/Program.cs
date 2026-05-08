using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForITMTicketsGlobal2024!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ITM-Tickets-Global";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ITM-Tickets-App";
var jwtExpiryMinutes = int.Parse(builder.Configuration["Jwt:ExpiryMinutes"] ?? "60");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = signingKey
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "ITM-Tickets Global Auth Service");

app.MapPost("/auth/login", (LoginRequest request) =>
{
    if (request.Username == "admin" && request.Password == "admin123" ||
        request.Username == "user" && request.Password == "user123")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, request.Username),
            new Claim(ClaimTypes.Role, request.Username == "admin" ? "Admin" : "User"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwtExpiryMinutes),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );

        return Results.Ok(new AuthResponse(
            Token: new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt: token.ValidTo,
            Username: request.Username
        ));
    }

    return Results.Unauthorized();
});

app.MapPost("/auth/register", (RegisterRequest request) =>
{
    return Results.Ok(new { Message = $"User {request.Username} registered successfully", Success = true });
});

app.MapGet("/auth/validate", (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        return Results.Ok(new
        {
            Valid = true,
            Username = context.User.Identity.Name,
            Roles = context.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
        });
    }

    return Results.Unauthorized();
}).RequireAuthorization();

app.MapDefaultEndpoints();

app.Run();

record LoginRequest(string Username, string Password);
record RegisterRequest(string Username, string Password, string Email);
record AuthResponse(string Token, DateTime ExpiresAt, string Username);
