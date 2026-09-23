using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Product.Domain.Repositories;
using Product.Infrastructure.Messaging;
using Product.Infrastructure.Persistence;
using Product.Infrastructure.Persistence.Repositories;

namespace Product.Infrastructure;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddProductInfrastructure(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default");

        builder.Services.AddScoped<IIntegrationEventMapper, ProductIntegrationEventMapper>();

        builder.Services.AddDbContext<ProductDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "catalog");
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            });

            // Thứ tự quan trọng: outbox ghi trước khi commit, dispatch chạy sau khi commit.
            options.AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<OutboxDomainEventInterceptor>(),
                sp.GetRequiredService<DomainEventDispatchInterceptor>());

            if (builder.Environment.IsDevelopment()) options.EnableSensitiveDataLogging();
        });

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ProductDbContext>());
        builder.Services.AddScoped<IProductRepository, ProductRepository>();
        builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();

        builder.Services.AddHealthChecks().AddNpgSql(connectionString, name: "postgres",
            tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));

        return builder;
    }
}
