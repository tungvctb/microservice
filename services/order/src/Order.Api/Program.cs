using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Infrastructure;
using Order.Api.Grpc;
using Order.Application;
using Order.Infrastructure;
using Order.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddServiceDefaults("order-service")
    .AddJwtAuthentication()
    .AddRedisInfrastructure("order")
    .AddKafkaMessaging(
        consumerGroupId: "order-service",
        // Nghe kết quả thanh toán để xác nhận hoặc bồi hoàn đơn.
        topics: new[] { KafkaTopics.PaymentEvents },
        typeof(Order.Application.DependencyInjection).Assembly)
    .AddOrderInfrastructure()
    .AddOutboxProcessing<OrderDbContext>();

builder.Services.AddOrderApplication();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.UseServiceDefaults();
app.MapGrpcService<OrderGrpcEndpoint>();
if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

await app.Services.InitializeOrderDatabaseAsync();

Log.Information("order-service sẵn sàng — điều phối saga đặt hàng");
await app.RunAsync();

public partial class Program;
