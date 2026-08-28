using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Stalksville.Api.Middleware;
using Stalksville.Application;
using Stalksville.Infrastructure;
using Stalksville.Infrastructure.Seeding;
using Stalksville.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStalksvilleInfrastructure(builder.Configuration);
builder.Services.AddStalksvilleApplication();
builder.Services.AddControllers();

// ---- JWT authentication ----
// The signing key comes from configuration (Auth:JwtKey). In Development an ephemeral key is
// generated per start (tokens die on restart); production must configure a persistent key.
var jwtKey = builder.Configuration["Auth:JwtKey"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    Console.WriteLine(
        builder.Environment.IsDevelopment()
            ? "WARN: Auth:JwtKey not configured — using an ephemeral key (tokens invalidate on every restart)."
            : "WARN: Auth:JwtKey not configured — PRODUCTION should configure a persistent key.");
}

var expireMinutes = builder.Configuration.GetValue("Auth:ExpireMinutes", 720);
builder.Services.AddSingleton<ITokenService>(new JwtTokenService(jwtKey, expireMinutes));

builder.Services
    .AddAuthentication("Smart")
    // X-Api-Key requests authenticate through the API-key handler, everything else through JWT.
    .AddPolicyScheme("Smart", "JWT or API key", o =>
        o.ForwardDefaultSelector = ctx =>
            ctx.Request.Headers.ContainsKey(ApiKeyAuthHandler.HeaderName)
                ? ApiKeyAuthHandler.SchemeName
                : JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep short claim names (sub/name/role) instead of the legacy WS-* URI mapping,
        // so RequireRole("ADMIN") matches the "role" claim issued by JwtTokenService.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "stalksville",
            ValidateAudience = true,
            ValidAudience = "stalksville-web",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = "role",
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    })
    .AddScheme<Stalksville.Api.Security.ApiKeyOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    // Everything requires authentication unless explicitly [AllowAnonymous].
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(Policies.Analyst, policy => policy.RequireRole("ANALYST", "ADMIN"));
    options.AddPolicy(Policies.Admin, policy => policy.RequireRole("ADMIN"));
});

// ---- OpenTelemetry: active only when an OTLP endpoint is configured (e.g. a collector) ----
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("stalksville-api"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation())
        .UseOtlpExporter();
    builder.Logging.AddOpenTelemetry(logging => logging.IncludeScopes = true);
}

// ---- Internal rate limiting: 100 requests / minute / user ----
// Development (and the e2e suite) legitimately bursts far higher than a human analyst,
// so the window is relaxed there; production keeps the strict limit.
var rateLimitPerWindow = builder.Environment.IsDevelopment() ? 1_000 : 100;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitPerWindow,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Startup diagnostics: show the effective configuration (never secrets).
var dbBuilder = new Npgsql.NpgsqlConnectionStringBuilder(
    app.Configuration.GetConnectionString("Database") ?? string.Empty);
Console.WriteLine(
    $"Stalksville API starting: environment={app.Environment.EnvironmentName}, " +
    $"database={dbBuilder.Database}@{dbBuilder.Host}:{dbBuilder.Port}, " +
    $"wolvesvilleMode={app.Configuration["Wolvesville:Mode"]}");

app.UseMiddleware<ExceptionMappingMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("api");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/v1.json");
    app.MapScalarApiReference(); // /scalar/v1
}

// Apply migrations + seed the admin user.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<StalksvilleSeeder>().SeedAsync();
}

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
