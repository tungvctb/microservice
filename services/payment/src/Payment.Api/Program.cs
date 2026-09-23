using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Infrastructure;
using Payment.Api.Grpc;
using Payment.Application;
using Payment.Infrastructure;
using Payment.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddServiceDefaults("payment-service")
    .AddJwtAuthentication()
    .AddRedisInfrastructure("payment")
    .AddKafkaMessaging(
        consumerGroupId: "payment-service",
        // Nghe đơn hàng mới để tự động thu tiền.
        topics: new[] { KafkaTopics.OrderEvents },
        typeof(Payment.Application.DependencyInjection).Assembly)
    .AddPaymentInfrastructure()
    .AddOutboxProcessing<PaymentDbContext>();

builder.Services.AddPaymentApplication();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.UseServiceDefaults();
app.MapGrpcService<PaymentGrpcEndpoint>();
if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

await app.Services.InitializePaymentDatabaseAsync();

Log.Information("payment-service sẵn sàng");
await app.RunAsync();

public partial class Program;
