using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Infrastructure;
using Inventory.Api.Grpc;
using Inventory.Application;
using Inventory.Infrastructure;
using Inventory.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddServiceDefaults("inventory-service")
    .AddJwtAuthentication()
    .AddRedisInfrastructure("inventory")
    .AddKafkaMessaging(
        consumerGroupId: "inventory-service",
        // Nghe catalog (sản phẩm mới), payment (chốt kho) và order (bồi hoàn khi hủy).
        topics: new[] { KafkaTopics.ProductEvents, KafkaTopics.PaymentEvents, KafkaTopics.OrderEvents },
        typeof(Inventory.Application.DependencyInjection).Assembly)
    .AddInventoryInfrastructure()
    .AddOutboxProcessing<InventoryDbContext>();

builder.Services.AddInventoryApplication();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.UseServiceDefaults();
app.MapGrpcService<InventoryGrpcEndpoint>();
if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

await InventoryDbInitializer.InitializeAsync(app.Services);

Log.Information("inventory-service sẵn sàng");
await app.RunAsync();

public partial class Program;
