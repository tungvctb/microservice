using BuildingBlocks.Core.Domain;
using BuildingBlocks.Core.Exceptions;

namespace Identity.Domain.Entities;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Customer = "Customer";

    public static readonly string[] All = { Admin, Manager, Customer };
}

public sealed class User : AggregateRoot
{
    private readonly List<string> _roles = new();
    private readonly List<RefreshToken> _refreshTokens = new();

    private User() { }

    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAtUtc { get; private set; }

    public IReadOnlyCollection<string> Roles => _roles.AsReadOnly();
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public static User Create(string email, string passwordHash, string fullName,
        string? phone = null, params string[] roles)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("user.invalid_email", "Email không hợp lệ.");

        var user = new User
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Phone = phone?.Trim()
        };

        user._roles.AddRange(roles.Length > 0 ? roles.Distinct() : new[] { UserRoles.Customer });
        return user;
    }

    public void UpdateProfile(string fullName, string? phone)
    {
        FullName = fullName.Trim();
        Phone = phone?.Trim();
        Touch();
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        // Đổi mật khẩu thì mọi phiên cũ phải chết — nếu không, kẻ đã chiếm tài khoản vẫn giữ quyền.
        foreach (var token in _refreshTokens) token.Revoke("Đổi mật khẩu");
        Touch();
    }

    public void SetRoles(IEnumerable<string> roles)
    {
        var valid = roles.Where(r => UserRoles.All.Contains(r)).Distinct().ToList();
        if (valid.Count == 0)
            throw new DomainException("user.invalid_roles", "Phải có ít nhất 1 vai trò hợp lệ.");

        _roles.Clear();
        _roles.AddRange(valid);
        Touch();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        if (!isActive) foreach (var token in _refreshTokens) token.Revoke("Tài khoản bị khóa");
        Touch();
    }

    public RefreshToken IssueRefreshToken(string token, DateTime expiresAtUtc)
    {
        var refreshToken = RefreshToken.Create(Id, token, expiresAtUtc);
        _refreshTokens.Add(refreshToken);
        LastLoginAtUtc = DateTime.UtcNow;
        Touch();
        return refreshToken;
    }

    public RefreshToken? FindActiveRefreshToken(string token) =>
        _refreshTokens.FirstOrDefault(t => t.Token == token && t.IsActive);

    public void RevokeAllRefreshTokens(string reason)
    {
        foreach (var token in _refreshTokens.Where(t => t.IsActive)) token.Revoke(reason);
        Touch();
    }
}

public sealed class RefreshToken : Entity
{
    private RefreshToken() { }

    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevokedReason { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsActive => RevokedAtUtc is null && !IsExpired;

    internal static RefreshToken Create(Guid userId, string token, DateTime expiresAtUtc) => new()
    {
        UserId = userId,
        Token = token,
        ExpiresAtUtc = expiresAtUtc
    };

    internal void Revoke(string reason)
    {
        if (RevokedAtUtc is not null) return;
        RevokedAtUtc = DateTime.UtcNow;
        RevokedReason = reason;
    }
}
