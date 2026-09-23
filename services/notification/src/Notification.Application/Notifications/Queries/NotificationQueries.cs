using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.Security;
using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Core.Results;
using Notification.Application.Common;
using Notification.Domain.Enums;
using Notification.Domain.Errors;
using Notification.Domain.Repositories;

namespace Notification.Application.Notifications.Queries;

public sealed record GetMyNotificationsQuery(
    bool? IsRead = null, NotificationSeverity? Severity = null,
    int Page = 1, int PageSize = 20) : IQuery<PagedResult<NotificationDto>>;

internal sealed class GetMyNotificationsHandler(INotificationRepository notifications, ICurrentUser currentUser)
    : IQueryHandler<GetMyNotificationsQuery, PagedResult<NotificationDto>>
{
    public async Task<Result<PagedResult<NotificationDto>>> Handle(
        GetMyNotificationsQuery query, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Result.Failure<PagedResult<NotificationDto>>(NotificationErrors.Unauthenticated);

        var page = await notifications.SearchAsync(new NotificationFilter
        {
            RecipientId = currentUser.UserId,
            IsRead = query.IsRead,
            Severity = query.Severity,
            Page = query.Page,
            PageSize = query.PageSize
        }, ct);

        return Result.Success(new PagedResult<NotificationDto>(
            page.Items.Select(n => n.ToDto()).ToList(), page.Page, page.PageSize, page.TotalCount));
    }
}

public sealed record GetUnreadCountQuery : IQuery<int>;

internal sealed class GetUnreadCountHandler(INotificationRepository notifications, ICurrentUser currentUser)
    : IQueryHandler<GetUnreadCountQuery, int>
{
    public async Task<Result<int>> Handle(GetUnreadCountQuery query, CancellationToken ct)
    {
        if (currentUser.UserId is null) return Result.Failure<int>(NotificationErrors.Unauthenticated);
        return Result.Success(await notifications.CountUnreadAsync(currentUser.UserId.Value, ct));
    }
}
