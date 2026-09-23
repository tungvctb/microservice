using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "api-gateway")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [gateway] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"));

// --- Xác thực: gateway kiểm JWT một lần, service phía sau tin vào kết quả đó ---
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    // SignalR đi qua gateway: token nằm ở query string vì WebSocket không gửi được header.
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) &&
                context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders("X-Correlation-Id")));

// --- Rate limit: chặn abuse ngay ở biên, service bên trong không phải gánh ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("per-ip", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Đăng nhập/đăng ký siết chặt hơn để cản dò mật khẩu.
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();

var app = builder.Build();

// Correlation id sinh tại biên và được YARP chuyển tiếp xuống mọi service.
app.Use(async (context, next) =>
{
    const string headerName = "X-Correlation-Id";
    var correlationId = context.Request.Headers.TryGetValue(headerName, out var incoming)
                        && !string.IsNullOrWhiteSpace(incoming)
        ? incoming.ToString()
        : Guid.NewGuid().ToString("N");

    context.Request.Headers[headerName] = correlationId;
    context.Response.Headers[headerName] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});

app.UseSerilogRequestLogging(o => o.MessageTemplate =
    "{RequestMethod} {RequestPath} → {StatusCode} trong {Elapsed:0.0}ms");

app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "api-gateway",
    routes = new[]
    {
        "/api/auth/*", "/api/users/*", "/api/products/*", "/api/categories/*",
        "/api/stocks/*", "/api/reservations/*", "/api/orders/*",
        "/api/payments/*", "/api/notifications/*", "/hubs/notifications"
    }
}));

app.MapHealthChecks("/health/live");

/// Gom health của toàn bộ service phía sau vào 1 endpoint cho dashboard.
app.MapGet("/health/services", async (IHttpClientFactory factory, IConfiguration config, CancellationToken ct) =>
{
    var targets = config.GetSection("HealthTargets").Get<Dictionary<string, string>>() ?? new();
    using var client = factory.CreateClient();
    client.Timeout = TimeSpan.FromSeconds(3);

    var checks = await Task.WhenAll(targets.Select(async target =>
    {
        try
        {
            var response = await client.GetAsync($"{target.Value}/health/ready", ct);
            return new { service = target.Key, healthy = response.IsSuccessStatusCode, status = (int)response.StatusCode };
        }
        catch
        {
            return new { service = target.Key, healthy = false, status = 0 };
        }
    }));

    var allHealthy = checks.All(c => c.healthy);
    return Results.Json(new { healthy = allHealthy, services = checks },
        statusCode: allHealthy ? 200 : 503);
});

app.MapReverseProxy();

Log.Information("api-gateway sẵn sàng trên {Urls}", string.Join(", ", app.Urls));
await app.RunAsync();
