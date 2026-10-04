public class JwtSettings
{
    public string Key { get; set; } = string.Empty;      // secret used to sign tokens (>= 32 chars)
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
