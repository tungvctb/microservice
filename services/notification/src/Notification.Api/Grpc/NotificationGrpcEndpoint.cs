using BuildingBlocks.Application.Cqrs;
using Grpc.Core;
using Notification.Application.Notifications.Commands;
using Notification.Domain.Enums;
using Notification.Domain.Repositories;
using Proto = BuildingBlocks.Contracts.Grpc.Notification;

namespace Notification.Api.Grpc;

/// <summary>
/// Cho phép service khác đẩy thông báo tức thì mà không cần đi vòng qua Kafka —
/// dùng khi độ trễ quan trọng hơn khả năng chịu lỗi.
/// </summary>
public sealed class NotificationGrpcEndpoint(
    IDispatcher dispatcher,
    INotificationRepository notifications) : Proto.NotificationGrpcService.NotificationGrpcServiceBase
{
    public override async Task<Proto.PushReply> PushToUser(Proto.PushToUserRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "userId không hợp lệ."));

        var severity = Enum.TryParse<NotificationSeverity>(request.Severity, true, out var s)
            ? s
            : NotificationSeverity.Info;

        var result = await dispatcher.Send(new CreateNotificationCommand(
            userId, request.Title, request.Body, severity,
            string.IsNullOrEmpty(request.Link) ? null : request.Link,
            request.Metadata.ToDictionary(kv => kv.Key, kv => kv.Value)), context.CancellationToken);

        return new Proto.PushReply
        {
            Delivered = result.IsSuccess,
            NotificationId = result.IsSuccess ? result.Value.Id.ToString() : string.Empty
        };
    }

    public override async Task<Proto.PushReply> Broadcast(Proto.BroadcastRequest request,
        ServerCallContext context)
    {
        var severity = Enum.TryParse<NotificationSeverity>(request.Severity, true, out var s)
            ? s
            : NotificationSeverity.Info;

        var result = await dispatcher.Send(new CreateNotificationCommand(
            RecipientId: null, request.Title, request.Body, severity,
            TargetRole: string.IsNullOrEmpty(request.Channel) ? null : request.Channel),
            context.CancellationToken);

        return new Proto.PushReply
        {
            Delivered = result.IsSuccess,
            NotificationId = result.IsSuccess ? result.Value.Id.ToString() : string.Empty
        };
    }

    public override async Task<Proto.UnreadCountReply> GetUnreadCount(Proto.GetUnreadCountRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "userId không hợp lệ."));

        var count = await notifications.CountUnreadAsync(userId, context.CancellationToken);
        return new Proto.UnreadCountReply { Count = count };
    }
}
