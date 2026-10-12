using System.Security.Claims;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Data;
using HotChocolate.Types;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public class Query
{
    [UsePaging(DefaultPageSize = 10, MaxPageSize = 50, IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Product> GetProducts([Service] AppDbContext db)
        => db.Products.AsNoTracking();

    public Task<ProductDto?> GetProduct(int id, [Service] IProductService products)
        => products.GetAsync(id);

    // هر کاربر لاگین‌شده‌ای می‌تونه اطلاعات خودش رو ببینه
    [Authorize]
    public async Task<UserDto?> GetMe(
        ClaimsPrincipal principal,
        [Service] UserManager<AppUser> users)
    {
        var user = await users.FindByIdAsync(principal.GetUserId().ToString());
        if (user is null) return null;
        return new UserDto(user.Id, user.FullName, user.Email!, (await users.GetRolesAsync(user)).ToList());
    }

    // فقط ادمین
    [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
    public Task<List<UserDto>> GetUsers([Service] IUserAdminService admin)
        => admin.GetAllAsync();

    // کاربر لاگین‌شده: سفارش‌های خودش (همون سرویسی که REST هم استفاده می‌کنه)
    [Authorize]
    public Task<List<OrderDto>> GetMyOrders(ClaimsPrincipal principal, [Service] IOrderService orders)
        => orders.GetMineAsync(principal.GetUserId());

    // ادمین: همه‌ی سفارش‌ها، با pagination و فیلتر و مرتب‌سازی (مثل محصولات)
    [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
    [UsePaging(DefaultPageSize = 20, MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Order> GetOrders([Service] AppDbContext db)
        => db.Orders.AsNoTracking();
}