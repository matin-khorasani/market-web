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

    public AdminUsersController(AppDbContext db, UserManager<AppUser> users)
    {
        _db = db;
        _users = users;
    }

    // Exactly 2 SQL queries no matter how many users exist (calling GetRolesAsync per user would be the N+1 problem)
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        var users = await _db.Users.AsNoTracking().ToListAsync();
        var roleRows = await (from ur in _db.UserRoles
                              join r in _db.Roles on ur.RoleId equals r.Id
                              select new { ur.UserId, RoleName = r.Name }).ToListAsync();
        var rolesByUser = roleRows.ToLookup(x => x.UserId, x => x.RoleName!);

        return users.Select(u => new UserDto(u.Id, u.FullName, u.Email!, rolesByUser[u.Id].ToList())).ToList();
    }

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

        await _users.DeleteAsync(user);   // the user's cart is deleted by cascade
        return NoContent();
    }
}
