using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly ITokenService _tokens;
    private readonly AppDbContext _db;

    public AuthController(UserManager<AppUser> users, ITokenService tokens, AppDbContext db)
    {
        _users = users;
        _tokens = tokens;
        _db = db;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterDto dto)
    {
        var user = new AppUser { UserName = dto.Email, Email = dto.Email, FullName = dto.FullName };
        var result = await _users.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        await _users.AddToRoleAsync(user, nameof(UserRole.Consumer));
        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginDto dto)
    {
        var user = await _users.FindByEmailAsync(dto.Email);
        if (user is null || !await _users.CheckPasswordAsync(user, dto.Password))
            return Unauthorized("Invalid email or password.");

        return Ok(await IssueTokensAsync(user));
    }

    // با refresh token معتبر، یک جفت توکن جدید می‌گیری (rotation: توکن قبلی باطل می‌شه)
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequestDto dto)
    {
        var hash = _tokens.Hash(dto.RefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is null)
            return Unauthorized("Invalid refresh token.");

        // توکنی که قبلاً باطل شده دوباره استفاده شده = احتمالاً دزدیده شده، پس همه‌ی توکن‌های کاربر باطل می‌شن
        if (stored.RevokedAtUtc is not null)
        {
            await RevokeAllForUserAsync(stored.UserId);
            return Unauthorized("Refresh token was already used. Please log in again.");
        }

        if (stored.ExpiresAtUtc <= DateTime.UtcNow)
            return Unauthorized("Refresh token expired.");

        var user = await _users.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
            return Unauthorized("Invalid refresh token.");

        return Ok(await IssueTokensAsync(user, replacing: stored));
    }

    // همیشه ۲۰۴ برمی‌گردونه تا معلوم نشه توکن واقعاً وجود داشته یا نه
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequestDto dto)
    {
        var hash = _tokens.Hash(dto.RefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is not null && stored.RevokedAtUtc is null)
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }

    private async Task<AuthResponse> IssueTokensAsync(AppUser user, RefreshToken? replacing = null)
    {
        var roles = await _users.GetRolesAsync(user);
        var (accessToken, accessExpires) = _tokens.CreateAccessToken(user, roles);
        var (refreshToken, refreshHash, refreshExpires) = _tokens.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = refreshExpires
        });

        if (replacing is not null)
        {
            replacing.RevokedAtUtc = DateTime.UtcNow;
            replacing.ReplacedByTokenHash = refreshHash;
        }

        await _db.SaveChangesAsync();   // توکن جدید و باطل‌شدن قدیمی در یک تراکنش
        return new AuthResponse(accessToken, accessExpires, refreshToken, user.Id, user.Email!, roles);
    }

    private async Task RevokeAllForUserAsync(int userId)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync();
        foreach (var t in active) t.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}