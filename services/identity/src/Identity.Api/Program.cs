using BuildingBlocks.Infrastructure;
using Identity.Application;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddServiceDefaults("identity-service")
    .AddJwtAuthentication()
    .AddRedisInfrastructure("identity")
    .AddKafkaMessaging(
        consumerGroupId: "identity-service",
        topics: Array.Empty<string>(),
        typeof(Identity.Application.DependencyInjection).Assembly)
    .AddIdentityInfrastructure()
    .AddOutboxProcessing<IdentityDbContext>();

builder.Services.AddIdentityApplication();

var app = builder.Build();

app.UseServiceDefaults();

await IdentityDbInitializer.InitializeAsync(app.Services,
    seed: builder.Configuration.GetValue("SeedData", true));

Log.Information("identity-service sẵn sàng");
await app.RunAsync();

public partial class Program;
