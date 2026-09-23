using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Infrastructure.Grpc;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Order.Application.Abstractions;
using Order.Domain.Repositories;
using Order.Infrastructure.Gateways;
using Order.Infrastructure.Messaging;
using Order.Infrastructure.Persistence;
using Order.Infrastructure.Persistence.Repositories;
using CatalogProto = BuildingBlocks.Contracts.Grpc.Product;
using InventoryProto = BuildingBlocks.Contracts.Grpc.Inventory;

namespace Order.Infrastructure;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddOrderInfrastructure(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default");

        builder.Services.AddScoped<IIntegrationEventMapper, OrderIntegrationEventMapper>();

        builder.Services.AddDbContext<OrderDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "ordering");
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            });

            options.AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<OutboxDomainEventInterceptor>(),
                sp.GetRequiredService<DomainEventDispatchInterceptor>());

            if (builder.Environment.IsDevelopment()) options.EnableSensitiveDataLogging();
        });

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<OrderDbContext>());
        builder.Services.AddScoped<IOrderRepository, OrderRepository>();

        // gRPC client tới các service khác — địa chỉ đọc từ cấu hình GrpcClients.
        builder.Services.AddGrpcServiceClient<CatalogProto.ProductGrpcService.ProductGrpcServiceClient>(
            builder.Configuration, "Product");
        builder.Services.AddGrpcServiceClient<InventoryProto.InventoryGrpcService.InventoryGrpcServiceClient>(
            builder.Configuration, "Inventory");

        builder.Services.AddScoped<ICatalogGateway, CatalogGrpcGateway>();
        builder.Services.AddScoped<IInventoryGateway, InventoryGrpcGateway>();

        builder.Services.AddHealthChecks().AddNpgSql(connectionString, name: "postgres",
            tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));

        return builder;
    }

    public static Task InitializeOrderDatabaseAsync(this IServiceProvider services) =>
        DatabaseInitializer.MigrateWithRetryAsync<OrderDbContext>(services);
}
