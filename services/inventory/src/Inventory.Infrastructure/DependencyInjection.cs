using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Inventory.Domain.Repositories;
using Inventory.Infrastructure.BackgroundJobs;
using Inventory.Infrastructure.Messaging;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Inventory.Infrastructure;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddInventoryInfrastructure(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default");

        builder.Services.AddScoped<IIntegrationEventMapper, InventoryIntegrationEventMapper>();

        builder.Services.AddDbContext<InventoryDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "inventory");
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            });

            options.AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<OutboxDomainEventInterceptor>(),
                sp.GetRequiredService<DomainEventDispatchInterceptor>());

            if (builder.Environment.IsDevelopment()) options.EnableSensitiveDataLogging();
        });

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<InventoryDbContext>());
        builder.Services.AddScoped<IStockRepository, StockRepository>();
        builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
        builder.Services.AddScoped<IStockMovementRepository, StockMovementRepository>();

        builder.Services.AddHostedService<ReservationExpiryJob>();
        builder.Services.AddHealthChecks().AddNpgSql(connectionString, name: "postgres",
            tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));

        return builder;
    }
}
