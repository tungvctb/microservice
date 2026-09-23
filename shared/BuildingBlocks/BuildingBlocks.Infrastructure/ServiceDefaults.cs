using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Messaging;
using BuildingBlocks.Application.Security;
using BuildingBlocks.Infrastructure.Grpc;
using BuildingBlocks.Infrastructure.Idempotency;
using BuildingBlocks.Infrastructure.Kafka;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Redis;
using BuildingBlocks.Infrastructure.Security;
using BuildingBlocks.Infrastructure.Web;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;

namespace BuildingBlocks.Infrastructure;

public static class ServiceDefaults
{
    public const string CorsPolicy = "frontend";

    /// <summary>Cấu hình nền chung cho mọi service: logging, correlation, swagger, CORS, health check.</summary>
    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, config) => config
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"));

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton<BuildingBlocks.Core.Abstractions.IDateTimeProvider,
            BuildingBlocks.Core.Abstractions.SystemDateTimeProvider>();

        builder.Services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo { Title = $"{serviceName} API", Version = "v1" });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Dán access token (không cần tiền tố 'Bearer')."
            });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? new[] { "http://localhost:5173" };

        builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()          // bắt buộc cho SignalR WebSocket
            .WithExposedHeaders(CorrelationMiddleware.HeaderName)));

        builder.Services.AddHealthChecks();
        builder.Services.AddSingleton<CorrelationClientInterceptor>();
        builder.Services.AddGrpc(o =>
        {
            o.EnableDetailedErrors = builder.Environment.IsDevelopment();
            o.Interceptors.Add<ServerLoggingInterceptor>();
        });

        return builder;
    }

    public static WebApplicationBuilder AddJwtAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // SignalR không gửi được header Authorization qua WebSocket — token đi trong query string.
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
        return builder;
    }

    /// <summary>Redis: cache-aside + distributed lock + backplane cho SignalR.</summary>
    public static WebApplicationBuilder AddRedisInfrastructure(this WebApplicationBuilder builder, string instanceName)
    {
        builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
        var redisConfig = builder.Configuration[$"{RedisOptions.SectionName}:ConnectionString"] ?? "localhost:6379";
        builder.Services.PostConfigure<RedisOptions>(o => o.InstanceName = instanceName);

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConfig);
            options.AbortOnConnectFail = false;   // service vẫn khởi động được khi Redis chưa sẵn sàng
            options.ConnectRetry = 5;
            options.ConnectTimeout = 5000;
            return ConnectionMultiplexer.Connect(options);
        });

        builder.Services.AddSingleton<ICacheService, RedisCacheService>();
        builder.Services.AddSingleton<IDistributedLock, RedisDistributedLock>();
        // Timeout bắt buộc: readiness probe phải trả lời trong thời gian có giới hạn,
        // nếu không orchestrator chỉ thấy "hết giờ" mà không biết thành phần nào hỏng.
        builder.Services.AddHealthChecks().AddRedis(redisConfig, name: "redis",
            tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));

        return builder;
    }

    /// <summary>Kafka: producer + consumer group riêng của service + auto provision topic ở dev.</summary>
    public static WebApplicationBuilder AddKafkaMessaging(this WebApplicationBuilder builder,
        string consumerGroupId, string[] topics, params Assembly[] handlerAssemblies)
    {
        builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
        builder.Services.PostConfigure<KafkaOptions>(o =>
        {
            o.ConsumerGroupId = consumerGroupId;
            o.Topics = topics;
        });

        builder.Services.AddSingleton<IEventBus, KafkaProducer>();
        builder.Services.AddSingleton<IntegrationEventDispatcher>();
        builder.Services.AddSingleton(sp =>
        {
            var registry = new IntegrationEventRegistry();
            foreach (var assembly in handlerAssemblies) registry.RegisterFrom(assembly);
            return registry;
        });

        foreach (var assembly in handlerAssemblies) RegisterEventHandlers(builder.Services, assembly);

        builder.Services.AddHostedService<KafkaTopicProvisioner>();
        if (topics.Length > 0) builder.Services.AddHostedService<KafkaConsumerService>();

        var bootstrap = builder.Configuration[$"{KafkaOptions.SectionName}:BootstrapServers"] ?? "localhost:9092";
        builder.Services.AddHealthChecks().AddKafka(
            new Confluent.Kafka.ProducerConfig
            {
                BootstrapServers = bootstrap,
                // Mặc định của librdkafka là 5 PHÚT — probe sẽ treo nếu broker chết.
                MessageTimeoutMs = 2000,
                SocketTimeoutMs = 2000
            },
            name: "kafka", tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));

        return builder;
    }

    private static void RegisterEventHandlers(IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        foreach (var itf in type.GetInterfaces().Where(i =>
                     i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
        {
            services.AddScoped(itf, type);
        }
    }

    /// <summary>Outbox processor + inbox store gắn với DbContext của service.</summary>
    public static WebApplicationBuilder AddOutboxProcessing<TContext>(this WebApplicationBuilder builder)
        where TContext : DbContext, IIntegrationDbContext
    {
        builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection(OutboxOptions.SectionName));
        builder.Services.AddScoped<IIntegrationDbContext>(sp => sp.GetRequiredService<TContext>());
        builder.Services.AddScoped<OutboxWriter>();
        builder.Services.AddScoped<IOutboxWriter>(sp => sp.GetRequiredService<OutboxWriter>());
        builder.Services.AddScoped<IIntegrationEventPublisher>(sp => sp.GetRequiredService<OutboxWriter>());
        builder.Services.AddScoped<IInboxStore, EfInboxStore<TContext>>();
        builder.Services.AddScoped<OutboxDomainEventInterceptor>();
        builder.Services.AddScoped<Persistence.DomainEventDispatchInterceptor>();
        builder.Services.AddSingleton<Persistence.AuditInterceptor>();
        builder.Services.AddHostedService<OutboxProcessor<TContext>>();
        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseSerilogRequestLogging(o => o.MessageTemplate =
            "{RequestMethod} {RequestPath} → {StatusCode} trong {Elapsed:0.0}ms");

        app.UseMiddleware<CorrelationMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(o => o.DisplayRequestDuration());
        }

        app.UseCors(CorsPolicy);
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapControllers();
        return app;
    }
}
