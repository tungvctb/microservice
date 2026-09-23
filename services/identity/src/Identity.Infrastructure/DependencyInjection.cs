using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Repositories;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BuildingBlocks.Core.Domain;
using BuildingBlocks.Contracts.IntegrationEvents;

namespace Identity.Infrastructure;

/// <summary>Identity chưa phát integration event nào — mapper trả null cho mọi domain event.</summary>
internal sealed class NoOpIntegrationEventMapper : IIntegrationEventMapper
{
    public IntegrationEvent? Map(IDomainEvent domainEvent) => null;
}

public static class DependencyInjection
{
    public static WebApplicationBuilder AddIdentityInfrastructure(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default");

        builder.Services.AddScoped<IIntegrationEventMapper, NoOpIntegrationEventMapper>();

        builder.Services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity");
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            });

            options.AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<OutboxDomainEventInterceptor>(),
                sp.GetRequiredService<DomainEventDispatchInterceptor>());

            if (builder.Environment.IsDevelopment()) options.EnableSensitiveDataLogging();
        });

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        builder.Services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();

        builder.Services.AddHealthChecks().AddNpgSql(connectionString, name: "postgres",
            tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));

        return builder;
    }
}
