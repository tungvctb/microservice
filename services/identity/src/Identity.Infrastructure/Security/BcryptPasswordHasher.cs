using Identity.Domain.Repositories;

namespace Identity.Infrastructure.Security;

/// <summary>BCrypt work factor 12 — chậm có chủ đích để brute-force trở nên tốn kém.</summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch (BCrypt.Net.SaltParseException) { return false; }
    }
}
