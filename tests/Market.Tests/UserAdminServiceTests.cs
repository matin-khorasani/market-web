namespace Market.Tests;

public class UserAdminServiceTests
{
    [Fact]
    public async Task ChangeRole_ToAdmin_ReplacesOldRole()
    {
        using var env = new TestEnv();
        var admin = await env.CreateUserAsync("admin@test.com", UserRole.Admin);
        var user = await env.CreateUserAsync();
        var service = new UserAdminService(env.Db, env.Users);

        var (found, error) = await service.ChangeRoleAsync(admin.Id, user.Id, UserRole.Admin);

        Assert.True(found);
        Assert.Null(error);
        Assert.Equal(new[] { "Admin" }, await env.Users.GetRolesAsync(user));
    }

    // باگ قبلی: نقش ۹۹ وجود نداره، ولی کاربر بدون هیچ نقشی می‌موند
    [Fact]
    public async Task ChangeRole_UndefinedRole_IsRejected_AndKeepsOldRole()
    {
        using var env = new TestEnv();
        var admin = await env.CreateUserAsync("admin@test.com", UserRole.Admin);
        var user = await env.CreateUserAsync();
        var service = new UserAdminService(env.Db, env.Users);

        var (_, error) = await service.ChangeRoleAsync(admin.Id, user.Id, (UserRole)99);

        Assert.NotNull(error);
        Assert.Equal(new[] { "Consumer" }, await env.Users.GetRolesAsync(user));
    }

    [Fact]
    public async Task ChangeRole_OwnAccount_IsRejected()
    {
        using var env = new TestEnv();
        var admin = await env.CreateUserAsync("admin@test.com", UserRole.Admin);
        var service = new UserAdminService(env.Db, env.Users);

        var (_, error) = await service.ChangeRoleAsync(admin.Id, admin.Id, UserRole.Consumer);

        Assert.NotNull(error);
        Assert.Equal(new[] { "Admin" }, await env.Users.GetRolesAsync(admin));
    }

    [Fact]
    public async Task ChangeRole_UnknownUser_ReturnsNotFound()
    {
        using var env = new TestEnv();
        var admin = await env.CreateUserAsync("admin@test.com", UserRole.Admin);
        var service = new UserAdminService(env.Db, env.Users);

        var (found, _) = await service.ChangeRoleAsync(admin.Id, 999, UserRole.Admin);

        Assert.False(found);
    }

    [Fact]
    public async Task Delete_UserWithOrders_IsRejected()
    {
        using var env = new TestEnv();
        var admin = await env.CreateUserAsync("admin@test.com", UserRole.Admin);
        var user = await env.CreateUserAsync();
        env.Db.Orders.Add(new Order { UserId = user.Id });
        await env.Db.SaveChangesAsync();
        var service = new UserAdminService(env.Db, env.Users);

        var (found, error) = await service.DeleteAsync(admin.Id, user.Id);

        Assert.True(found);
        Assert.NotNull(error);
        Assert.NotNull(await env.Users.FindByIdAsync(user.Id.ToString()));
    }

    [Fact]
    public async Task GetAll_ReturnsUsersWithTheirRoles()
    {
        using var env = new TestEnv();
        await env.CreateUserAsync("admin@test.com", UserRole.Admin);
        await env.CreateUserAsync("user@test.com", UserRole.Consumer);
        var service = new UserAdminService(env.Db, env.Users);

        var all = await service.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, u => u.Email == "admin@test.com" && u.Roles.Contains("Admin"));
    }
}
