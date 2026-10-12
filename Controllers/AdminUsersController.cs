using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IUserAdminService _admin;

    public AdminUsersController(AppDbContext db, UserManager<AppUser> users, IUserAdminService admin)
    {
        _db = db;
        _users = users;
        _admin = admin;
    }

    // Exactly 2 SQL queries no matter how many users exist (calling GetRolesAsync per user would be the N+1 problem)
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll() => await _admin.GetAllAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> Get(int id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        return new UserDto(user.Id, user.FullName, user.Email!, (await _users.GetRolesAsync(user)).ToList());
    }

    // PUT api/admin/users/5/role   { "role": "Admin" }
    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id, ChangeRoleDto dto)
    {
        if (id == User.GetUserId()) return BadRequest("You cannot change your own role.");

        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        await _users.RemoveFromRolesAsync(user, await _users.GetRolesAsync(user));
        await _users.AddToRoleAsync(user, dto.Role.ToString());
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (id == User.GetUserId()) return BadRequest("You cannot delete yourself.");

        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (await _db.Orders.AnyAsync(o => o.UserId == id))
            return BadRequest("This user has orders and cannot be deleted.");
        await _users.DeleteAsync(user);   // the user's cart is deleted by cascade
        return NoContent();
    }
}
