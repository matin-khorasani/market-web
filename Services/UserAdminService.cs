using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public interface IUserAdminService
{
    Task<List<UserDto>> GetAllAsync();
    Task<UserDto?> GetAsync(int id);
    Task<(bool Found, string? Error)> ChangeRoleAsync(int actingUserId, int id, UserRole role);
    Task<(bool Found, string? Error)> DeleteAsync(int actingUserId, int id);
}

public class UserAdminService : IUserAdminService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;

    public UserAdminService(AppDbContext db, UserManager<AppUser> users)
    {
        _db = db;
        _users = users;
    }

    // Exactly 2 SQL queries no matter how many users exist (calling GetRolesAsync per user would be the N+1 problem)
    public async Task<List<UserDto>> GetAllAsync()
    {
        var users = await _db.Users.AsNoTracking().ToListAsync();
        var roleRows = await (from ur in _db.UserRoles
                              join r in _db.Roles on ur.RoleId equals r.Id
                              select new { ur.UserId, RoleName = r.Name }).ToListAsync();
        var rolesByUser = roleRows.ToLookup(x => x.UserId, x => x.RoleName!);

        return users.Select(u => new UserDto(u.Id, u.FullName, u.Email!, rolesByUser[u.Id].ToList())).ToList();
    }

    public async Task<UserDto?> GetAsync(int id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return null;
        return new UserDto(user.Id, user.FullName, user.Email!, (await _users.GetRolesAsync(user)).ToList());
    }

    public async Task<(bool Found, string? Error)> ChangeRoleAsync(int actingUserId, int id, UserRole role)
    {
        if (id == actingUserId) return (true, "You cannot change your own role.");
        // JSON اجازه می‌ده عدد دلخواه (مثلاً 99) برای enum بیاد؛ اینجا رد می‌شه
        if (!Enum.IsDefined(role)) return (true, "Unknown role.");

        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return (false, null);

        var newRole = role.ToString();
        var current = await _users.GetRolesAsync(user);

        // اول نقش جدید اضافه می‌شه و فقط اگه موفق بود نقش‌های قبلی حذف می‌شن؛
        // پس هیچ خطایی نمی‌تونه کاربر رو بدون نقش باقی بذاره.
        if (!current.Contains(newRole))
        {
            var add = await _users.AddToRoleAsync(user, newRole);
            if (!add.Succeeded) return (true, Describe(add));
        }

        var toRemove = current.Where(r => r != newRole).ToList();
        if (toRemove.Count > 0)
        {
            var remove = await _users.RemoveFromRolesAsync(user, toRemove);
            if (!remove.Succeeded) return (true, Describe(remove));
        }

        return (true, null);
    }

    public async Task<(bool Found, string? Error)> DeleteAsync(int actingUserId, int id)
    {
        if (id == actingUserId) return (true, "You cannot delete yourself.");

        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return (false, null);
        if (await _db.Orders.AnyAsync(o => o.UserId == id))
            return (true, "This user has orders and cannot be deleted.");

        var result = await _users.DeleteAsync(user);   // the user's cart is deleted by cascade
        return result.Succeeded ? (true, null) : (true, Describe(result));
    }

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));
}
