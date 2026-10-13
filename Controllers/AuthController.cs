using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

// Controller فقط ورودی رو به AuthService می‌ده و نتیجه رو به پاسخ HTTP تبدیل می‌کنه.
// همه‌ی خطاها ProblemDetails هستن (مثل بقیه‌ی API).
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterDto dto) =>
        ToActionResult(await _auth.RegisterAsync(dto));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginDto dto) =>
        ToActionResult(await _auth.LoginAsync(dto));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequestDto dto) =>
        ToActionResult(await _auth.RefreshAsync(dto.RefreshToken));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequestDto dto)
    {
        await _auth.LogoutAsync(dto.RefreshToken);
        return NoContent();
    }

    private ActionResult ToActionResult(AuthResult r) => r.Error switch
    {
        AuthError.None => Ok(r.Response),
        AuthError.Validation => Problem(statusCode: 400, title: "Registration failed.", detail: r.Message),
        AuthError.InvalidCredentials => Problem(statusCode: 401, title: "Invalid email or password."),
        AuthError.LockedOut => Problem(statusCode: 429, title: "Too many failed attempts. Try again later."),
        AuthError.InvalidToken => Problem(statusCode: 401, title: "Invalid refresh token."),
        AuthError.TokenReused => Problem(statusCode: 401, title: "Refresh token was already used. Please log in again."),
        AuthError.TokenExpired => Problem(statusCode: 401, title: "Refresh token expired."),
        _ => Problem(statusCode: 500, title: "An unexpected error occurred.")
    };
}
