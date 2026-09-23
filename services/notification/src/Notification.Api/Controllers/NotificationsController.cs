using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.Application.Common;
using Notification.Application.Notifications.Commands;
using Notification.Application.Notifications.Queries;

namespace Notification.Api.Controllers;

[Route("api/notifications")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationsController : ApiControllerBase
{
    /// <summary>Thông báo của tôi (gồm cả broadcast), mới nhất trước.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine([FromQuery] GetMyNotificationsQuery query, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(query, ct));

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(new GetUnreadCountQuery(), ct));

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new MarkNotificationReadCommand(id), ct));

    [HttpPost("read-all")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new MarkAllNotificationsReadCommand(), ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new DeleteNotificationCommand(id), ct));

    /// <summary>Gửi thông báo thủ công — tiện để thử realtime mà không cần đặt đơn thật.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateNotificationCommand command, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(command, ct));
}
