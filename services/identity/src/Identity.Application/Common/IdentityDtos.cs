using Identity.Domain.Entities;

namespace Identity.Application.Common;

public sealed record AuthResultDto(
    string AccessToken, string RefreshToken, int ExpiresInSeconds, UserDto User);

public sealed record UserDto(
    Guid Id, string Email, string FullName, string? Phone,
    IReadOnlyList<string> Roles, bool IsActive,
    DateTime CreatedAtUtc, DateTime? LastLoginAtUtc);

public static class UserMapping
{
    public static UserDto ToDto(this User u) => new(
        u.Id, u.Email, u.FullName, u.Phone, u.Roles.ToList(), u.IsActive, u.CreatedAtUtc, u.LastLoginAtUtc);
}
