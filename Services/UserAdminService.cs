using Microsoft.EntityFrameworkCore;

public interface IUserAdminService
{
    Task<List<UserDto>> GetAllAsync();
}

public class UserAdminService : IUserAdminService
{
    private readonly AppDbContext _db;
    public UserAdminService(AppDbContext db) => _db = db;
    public async Task<List<UserDto>> GetAllAsync()
    {
        var users = await _db.Users.AsNoTracking().ToListAsync();
        var roleRows = await (from ur in _db.UserRoles
                              join r in _db.Roles on ur.RoleId equals r.Id
                              select new { ur.UserId, RoleName = r.Name }).ToListAsync();
        var rolesByUser = roleRows.ToLookup(x => x.UserId, x => x.RoleName!);

        return users.Select(u => new UserDto(u.Id, u.FullName, u.Email!, rolesByUser[u.Id].ToList())).ToList();
    }
}