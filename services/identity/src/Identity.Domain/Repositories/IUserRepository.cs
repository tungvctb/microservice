using BuildingBlocks.Core.Pagination;
using Identity.Domain.Entities;

namespace Identity.Domain.Repositories;

public sealed record UserFilter : PageRequest
{
    public string? Search { get; init; }
    public string? Role { get; init; }
    public bool? IsActive { get; init; }
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByRefreshTokenAsync(string token, CancellationToken ct = default);
    Task<PagedResult<User>> SearchAsync(UserFilter filter, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    void Add(User user);
}

/// <summary>Băm và kiểm tra mật khẩu — tách ra để domain không phụ thuộc thư viện crypto.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenGenerator
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    int AccessTokenMinutes { get; }
    int RefreshTokenDays { get; }
}
