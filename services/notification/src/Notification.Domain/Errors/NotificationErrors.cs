using BuildingBlocks.Core.Results;

namespace Notification.Domain.Errors;

public static class NotificationErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("notification.not_found", $"Không tìm thấy thông báo {id}.");

    public static readonly Error Forbidden =
        Error.Forbidden("notification.forbidden", "Bạn không có quyền với thông báo này.");

    public static readonly Error Unauthenticated =
        Error.Unauthorized("notification.unauthenticated", "Cần đăng nhập để xem thông báo.");
}
