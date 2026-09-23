using BuildingBlocks.Contracts.IntegrationEvents;
using BuildingBlocks.Infrastructure;
using Notification.Api.Grpc;
using Notification.Application;
using Notification.Infrastructure;
using Notification.Infrastructure.Realtime;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddServiceDefaults("notification-service")
    .AddJwtAuthentication()
    .AddRedisInfrastructure("notification")
    .AddKafkaMessaging(
        consumerGroupId: "notification-service",
        // Nghe TẤT CẢ topic nghiệp vụ — đây là điểm hội tụ của hệ thống.
        topics: new[]
        {
            KafkaTopics.OrderEvents,
            KafkaTopics.PaymentEvents,
            KafkaTopics.InventoryEvents,
            KafkaTopics.NotificationCommands
        },
        typeof(Notification.Application.DependencyInjection).Assembly)
    .AddNotificationInfrastructure()
    .AddOutboxProcessing<Notification.Infrastructure.Persistence.NotificationDbContext>();

builder.Services.AddNotificationApplication();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.UseServiceDefaults();
app.MapHub<NotificationHub>(NotificationHub.Path);
app.MapGrpcService<NotificationGrpcEndpoint>();
if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

await app.Services.InitializeNotificationDatabaseAsync();

Log.Information("notification-service sẵn sàng | SignalR hub: {Path}", NotificationHub.Path);
await app.RunAsync();

public partial class Program;
