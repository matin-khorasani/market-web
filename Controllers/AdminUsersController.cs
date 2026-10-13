using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminUsersController : ControllerBase
{
    private readonly IUserAdminService _admin;
    public AdminUsersController(IUserAdminService admin) => _admin = admin;

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll() => await _admin.GetAllAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> Get(int id)
    {
        var user = await _admin.GetAsync(id);
        return user is null ? NotFound() : user;
    }

    // PUT api/admin/users/5/role   { "role": "Admin" }
    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id, ChangeRoleDto dto)
    {
        var (found, error) = await _admin.ChangeRoleAsync(User.GetUserId(), id, dto.Role);
        if (!found) return NotFound();
        if (error is not null) return Problem(statusCode: 400, title: error);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (found, error) = await _admin.DeleteAsync(User.GetUserId(), id);
        if (!found) return NotFound();
        if (error is not null) return Problem(statusCode: 400, title: error);
        return NoContent();
    }
}
