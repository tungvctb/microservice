namespace BuildingBlocks.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "ecommerce-identity";
    public string Audience { get; set; } = "ecommerce-api";
    /// <summary>Khóa ký HMAC. Môi trường thật phải nạp từ secret store, không để trong appsettings.</summary>
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
}
