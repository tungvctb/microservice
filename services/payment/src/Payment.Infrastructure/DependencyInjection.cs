using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Payment.Domain.Repositories;
using Payment.Infrastructure.Gateways;
using Payment.Infrastructure.Messaging;
using Payment.Infrastructure.Persistence;
using Payment.Infrastructure.Persistence.Repositories;

namespace Payment.Infrastructure;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddPaymentInfrastructure(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default");

        builder.Services.Configure<PaymentGatewayOptions>(
            builder.Configuration.GetSection(PaymentGatewayOptions.SectionName));

        builder.Services.AddScoped<IIntegrationEventMapper, PaymentIntegrationEventMapper>();
        builder.Services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();

        builder.Services.AddDbContext<PaymentDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "payment");
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            });

            options.AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<OutboxDomainEventInterceptor>(),
                sp.GetRequiredService<DomainEventDispatchInterceptor>());

            if (builder.Environment.IsDevelopment()) options.EnableSensitiveDataLogging();
        });

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PaymentDbContext>());
        builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

        builder.Services.AddHealthChecks().AddNpgSql(connectionString, name: "postgres",
            tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));

        return builder;
    }

    public static Task InitializePaymentDatabaseAsync(this IServiceProvider services) =>
        DatabaseInitializer.MigrateWithRetryAsync<PaymentDbContext>(services);
}
