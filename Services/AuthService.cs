using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public enum AuthError
{
    None,
    Validation,
    InvalidCredentials,
    LockedOut,
    InvalidToken,
    TokenReused,
    TokenExpired
}

// نتیجه‌ی یک عملیات احراز هویت: یا موفق (Response) یا یک نوع خطا. تبدیل خطا به status code کار controller است.
public class AuthResult
{
    public AuthResponse? Response { get; private init; }
    public AuthError Error { get; private init; }
    public string? Message { get; private init; }
    public bool Succeeded => Error == AuthError.None;

    public static AuthResult Ok(AuthResponse response) => new() { Response = response };
    public static AuthResult Fail(AuthError error, string? message = null) => new() { Error = error, Message = message };
}

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterDto dto);
    Task<AuthResult> LoginAsync(LoginDto dto);
    Task<AuthResult> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
}

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _users;
    private readonly ITokenService _tokens;
    private readonly AppDbContext _db;

    public AuthService(UserManager<AppUser> users, ITokenService tokens, AppDbContext db)
    {
        _users = users;
        _tokens = tokens;
        _db = db;
    }

    public async Task<AuthResult> RegisterAsync(RegisterDto dto)
    {
        var user = new AppUser { UserName = dto.Email, Email = dto.Email, FullName = dto.FullName };
        var result = await _users.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return AuthResult.Fail(AuthError.Validation, string.Join(" ", result.Errors.Select(e => e.Description)));

        var roleResult = await _users.AddToRoleAsync(user, nameof(UserRole.Consumer));
        if (!roleResult.Succeeded)
        {
            // کاربر بدون نقش نباید باقی بمونه
            await _users.DeleteAsync(user);
            throw new InvalidOperationException("Could not assign the default role to the new user.");
        }

        return AuthResult.Ok(await IssueTokensAsync(user));
    }

    public async Task<AuthResult> LoginAsync(LoginDto dto)
    {
        var user = await _users.FindByEmailAsync(dto.Email);
        if (user is null)
        {
            // یک hash بی‌مصرف انجام می‌دیم تا زمان پاسخ نشون نده ایمیل وجود داره یا نه
            _users.PasswordHasher.HashPassword(new AppUser(), dto.Password);
            return AuthResult.Fail(AuthError.InvalidCredentials);
        }

        if (await _users.IsLockedOutAsync(user))
            return AuthResult.Fail(AuthError.LockedOut);

        if (!await _users.CheckPasswordAsync(user, dto.Password))
        {
            await _users.AccessFailedAsync(user);   // شمارنده‌ی تلاش ناموفق؛ بعد از ۵ بار حساب موقتاً قفل می‌شه
            return AuthResult.Fail(AuthError.InvalidCredentials);
        }

        await _users.ResetAccessFailedCountAsync(user);
        return AuthResult.Ok(await IssueTokensAsync(user));
    }

    // با refresh token معتبر، یک جفت توکن جدید می‌گیری (rotation: توکن قبلی باطل می‌شه)
    public async Task<AuthResult> RefreshAsync(string refreshToken)
    {
        var hash = _tokens.Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is null)
            return AuthResult.Fail(AuthError.InvalidToken);

        // توکنی که قبلاً باطل شده دوباره استفاده شده = احتمالاً دزدیده شده، پس همه‌ی توکن‌های کاربر باطل می‌شن
        if (stored.RevokedAtUtc is not null)
        {
            await RevokeAllForUserAsync(stored.UserId);
            return AuthResult.Fail(AuthError.TokenReused);
        }

        if (stored.ExpiresAtUtc <= DateTime.UtcNow)
            return AuthResult.Fail(AuthError.TokenExpired);

        var user = await _users.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
            return AuthResult.Fail(AuthError.InvalidToken);

        return AuthResult.Ok(await IssueTokensAsync(user, replacing: stored));
    }

    // controller همیشه ۲۰۴ برمی‌گردونه تا معلوم نشه توکن واقعاً وجود داشته یا نه
    public async Task LogoutAsync(string refreshToken)
    {
        var hash = _tokens.Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is not null && stored.RevokedAtUtc is null)
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
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
