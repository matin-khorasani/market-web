using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(AppUser user, IList<string> roles);
    (string Token, string TokenHash, DateTime ExpiresAtUtc) CreateRefreshToken();
    string Hash(string token);
}

public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;
    public TokenService(IOptions<JwtSettings> options) => _settings = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(AppUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email ?? ""),
            new("name", user.FullName),
            new("jti", Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    // یک رشته‌ی تصادفی امن (۶۴ بایت). این JWT نیست، فقط یک "شناسه‌ی مخفی" است.
    public (string Token, string TokenHash, DateTime ExpiresAtUtc) CreateRefreshToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (token, Hash(token), DateTime.UtcNow.AddDays(_settings.RefreshTokenDays));
    }

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}