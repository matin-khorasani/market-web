using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly ITokenService _tokens;

    public AuthController(UserManager<AppUser> users, ITokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    // Anyone can register, and always becomes a Consumer (admins are created by seed / by another admin)
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterDto dto)
    {
        var user = new AppUser { UserName = dto.Email, Email = dto.Email, FullName = dto.FullName };
        var result = await _users.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        await _users.AddToRoleAsync(user, nameof(UserRole.Consumer));
        return Ok(await BuildResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginDto dto)
    {
        var user = await _users.FindByEmailAsync(dto.Email);

        // Same message for "no such user" and "wrong password" so attackers can't discover which emails exist
        if (user is null || !await _users.CheckPasswordAsync(user, dto.Password))
            return Unauthorized("Invalid email or password.");

        return Ok(await BuildResponse(user));
    }

    private async Task<AuthResponse> BuildResponse(AppUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        var (token, expires) = _tokens.CreateAccessToken(user, roles);
        return new AuthResponse(token, expires, user.Id, user.Email!, roles);
    }
}
