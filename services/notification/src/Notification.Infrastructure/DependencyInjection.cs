using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notification.Application.Abstractions;
using Notification.Domain.Repositories;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Persistence.Repositories;
using Notification.Infrastructure.Realtime;

namespace Notification.Infrastructure;

internal sealed class NoOpIntegrationEventMapper : IIntegrationEventMapper
{
    public IntegrationEvent? Map(IDomainEvent domainEvent) => null;
}

public static class DependencyInjection
{
    public static WebApplicationBuilder AddNotificationInfrastructure(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default");

        builder.Services.AddScoped<IIntegrationEventMapper, NoOpIntegrationEventMapper>();

        builder.Services.AddDbContext<NotificationDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "notification");
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            });

            options.AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<OutboxDomainEventInterceptor>(),
                sp.GetRequiredService<DomainEventDispatchInterceptor>());

            if (builder.Environment.IsDevelopment()) options.EnableSensitiveDataLogging();
        });

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<NotificationDbContext>());
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        builder.Services.AddScoped<IRealtimePublisher, SignalRPublisher>();

        // Redis backplane: nhiều instance notification-service vẫn đẩy được tới đúng client.
        var redisConnection = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
        builder.Services
            .AddSignalR(o =>
            {
                o.EnableDetailedErrors = builder.Environment.IsDevelopment();
                o.KeepAliveInterval = TimeSpan.FromSeconds(15);
                o.ClientTimeoutInterval = TimeSpan.FromSeconds(45);
            })
            .AddStackExchangeRedis(redisConnection, o => o.Configuration.ChannelPrefix =
                StackExchange.Redis.RedisChannel.Literal("ecommerce-signalr"));

        builder.Services.AddHealthChecks().AddNpgSql(connectionString, name: "postgres",
            tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));

        return builder;
    }

    public static Task InitializeNotificationDatabaseAsync(this IServiceProvider services) =>
        DatabaseInitializer.MigrateWithRetryAsync<NotificationDbContext>(services);
}
