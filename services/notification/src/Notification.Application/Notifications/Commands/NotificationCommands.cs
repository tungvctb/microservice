using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.Security;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions;
using Notification.Application.Common;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Domain.Errors;
using Notification.Domain.Repositories;

namespace Notification.Application.Notifications.Commands;

public sealed record CreateNotificationCommand(
    Guid? RecipientId,
    string Title,
    string Body,
    NotificationSeverity Severity = NotificationSeverity.Info,
    string? Link = null,
    Dictionary<string, string>? Metadata = null,
    string? TargetRole = null) : ICommand<NotificationDto>;

public sealed class CreateNotificationValidator : AbstractValidator<CreateNotificationCommand>
{
    public CreateNotificationValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(1000);
    }
}

/// <summary>
/// Lưu thông báo rồi đẩy realtime. Thứ tự này quan trọng: nếu client offline,
/// thông báo vẫn còn trong DB để đọc khi quay lại.
/// </summary>
internal sealed class CreateNotificationHandler(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    IRealtimePublisher realtime,
    ILogger<CreateNotificationHandler> logger) : ICommandHandler<CreateNotificationCommand, NotificationDto>
{
    public async Task<Result<NotificationDto>> Handle(CreateNotificationCommand command, CancellationToken ct)
    {
        var notification = NotificationMessage.Create(command.RecipientId, command.Title, command.Body,
            command.Severity, command.Link, command.Metadata);

        notifications.Add(notification);
        await unitOfWork.SaveChangesAsync(ct);

        var dto = notification.ToDto();

        try
        {
            if (command.RecipientId is not null)
            {
                await realtime.PushToUserAsync(command.RecipientId.Value, dto, ct);

                var unread = await notifications.CountUnreadAsync(command.RecipientId.Value, ct);
                await realtime.PushUnreadCountAsync(command.RecipientId.Value, unread, ct);
            }
            else if (!string.IsNullOrEmpty(command.TargetRole))
            {
                await realtime.PushToRoleAsync(command.TargetRole, dto, ct);
            }
            else
            {
                await realtime.BroadcastAsync(dto, ct);
            }
        }
        catch (Exception ex)
        {
            // Đẩy realtime hỏng không được làm mất thông báo đã lưu.
            logger.LogWarning(ex, "Đẩy realtime thất bại cho thông báo {Id}", notification.Id);
        }

        return Result.Success(dto);
    }
}

public sealed record MarkNotificationReadCommand(Guid Id) : ICommand<NotificationDto>;

internal sealed class MarkNotificationReadHandler(
    INotificationRepository notifications, IUnitOfWork unitOfWork,
    ICurrentUser currentUser, IRealtimePublisher realtime)
    : ICommandHandler<MarkNotificationReadCommand, NotificationDto>
{
    public async Task<Result<NotificationDto>> Handle(MarkNotificationReadCommand command, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Result.Failure<NotificationDto>(NotificationErrors.Unauthenticated);

        var notification = await notifications.GetByIdAsync(command.Id, ct);
        if (notification is null)
            return Result.Failure<NotificationDto>(NotificationErrors.NotFound(command.Id));

        if (notification.RecipientId is not null && notification.RecipientId != currentUser.UserId)
            return Result.Failure<NotificationDto>(NotificationErrors.Forbidden);

        notification.MarkAsRead();
        await unitOfWork.SaveChangesAsync(ct);

        var unread = await notifications.CountUnreadAsync(currentUser.UserId.Value, ct);
        await realtime.PushUnreadCountAsync(currentUser.UserId.Value, unread, ct);

        return Result.Success(notification.ToDto());
    }
}

public sealed record MarkAllNotificationsReadCommand : ICommand<int>;

internal sealed class MarkAllNotificationsReadHandler(
    INotificationRepository notifications, ICurrentUser currentUser, IRealtimePublisher realtime)
    : ICommandHandler<MarkAllNotificationsReadCommand, int>
{
    public async Task<Result<int>> Handle(MarkAllNotificationsReadCommand command, CancellationToken ct)
    {
        if (currentUser.UserId is null) return Result.Failure<int>(NotificationErrors.Unauthenticated);

        var count = await notifications.MarkAllAsReadAsync(currentUser.UserId.Value, ct);
        await realtime.PushUnreadCountAsync(currentUser.UserId.Value, 0, ct);

        return Result.Success(count);
    }
}

public sealed record DeleteNotificationCommand(Guid Id) : ICommand<Unit>;

internal sealed class DeleteNotificationHandler(
    INotificationRepository notifications, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    : ICommandHandler<DeleteNotificationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteNotificationCommand command, CancellationToken ct)
    {
        if (currentUser.UserId is null) return Result.Failure<Unit>(NotificationErrors.Unauthenticated);

        var notification = await notifications.GetByIdAsync(command.Id, ct);
        if (notification is null) return Result.Failure<Unit>(NotificationErrors.NotFound(command.Id));

        if (notification.RecipientId != currentUser.UserId)
            return Result.Failure<Unit>(NotificationErrors.Forbidden);

        notifications.Remove(notification);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(Unit.Value);
    }
}
