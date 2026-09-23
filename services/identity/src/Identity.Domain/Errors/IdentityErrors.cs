using BuildingBlocks.Core.Results;

namespace Identity.Domain.Errors;

public static class IdentityErrors
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "Email hoặc mật khẩu không đúng.");

    public static readonly Error AccountDisabled =
        Error.Forbidden("auth.account_disabled", "Tài khoản đã bị khóa.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("auth.invalid_refresh_token", "Refresh token không hợp lệ hoặc đã hết hạn.");

    public static Error EmailTaken(string email) =>
        Error.Conflict("user.email_taken", $"Email '{email}' đã được đăng ký.");

    public static Error UserNotFound(Guid id) =>
        Error.NotFound("user.not_found", $"Không tìm thấy người dùng {id}.");

    public static readonly Error WrongCurrentPassword =
        Error.Validation("user.wrong_password", "Mật khẩu hiện tại không đúng.");
}
