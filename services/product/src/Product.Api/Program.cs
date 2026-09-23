using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Infrastructure;
using Product.Api.Grpc;
using Product.Application;
using Product.Infrastructure;
using Product.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddServiceDefaults("product-service")
    .AddJwtAuthentication()
    .AddRedisInfrastructure("product")
    .AddKafkaMessaging(
        consumerGroupId: "product-service",
        // Catalog chủ yếu publish; chưa cần subscribe topic nào.
        topics: Array.Empty<string>(),
        typeof(Program).Assembly)
    .AddProductInfrastructure()
    .AddOutboxProcessing<ProductDbContext>();

builder.Services.AddProductApplication();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.UseServiceDefaults();
app.MapGrpcService<ProductGrpcEndpoint>();
if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

await ProductDbInitializer.InitializeAsync(app.Services,
    seed: builder.Configuration.GetValue("SeedData", true));

Log.Information("product-service sẵn sàng | REST+gRPC trên cùng cổng (HTTP/2)");
await app.RunAsync();

// Cho phép integration test dựng host qua WebApplicationFactory.
public partial class Program;
