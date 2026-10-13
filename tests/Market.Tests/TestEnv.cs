using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Market.Tests;

// InMemory ستون rowversion رو خودش پر نمی‌کنه؛ پس توی تست‌ها این ستون‌ها یک ستون معمولی (nullable) می‌شن.
// (کنترل concurrency واقعی فقط با SQL Server قابل تست است.)
public class TestDbContext : AppDbContext
{
    public TestDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Product>().Property(p => p.RowVersion)
            .IsConcurrencyToken(false).ValueGeneratedNever().IsRequired(false);
        modelBuilder.Entity<Order>().Property(o => o.RowVersion)
            .IsConcurrencyToken(false).ValueGeneratedNever().IsRequired(false);
    }
}

// یک cache ساختگی در حافظه، تا بشه دید چه کلیدهایی باطل شدن
public class FakeCache : ICacheService
{
    public Dictionary<string, object> Store { get; } = new();
    public List<string> Removed { get; } = new();

    public Task<T?> GetAsync<T>(string key) =>
        Task.FromResult(Store.TryGetValue(key, out var v) ? (T?)v : default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        Store[key] = value!;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(params string[] keys)
    {
        foreach (var k in keys)
        {
            Store.Remove(k);
            Removed.Add(k);
        }
        return Task.CompletedTask;
    }
}

// هر تست یک دیتابیس InMemory تازه + UserManager/RoleManager واقعی دارد
public class TestEnv : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public AppDbContext Db { get; }
    public UserManager<AppUser> Users { get; }
    public FakeCache Cache { get; } = new();
    public ITokenService Tokens { get; } = new TokenService(Options.Create(new JwtSettings
    {
        Key = new string('k', 40),
        Issuer = "test",
        Audience = "test"
    }));

    public TestEnv()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<AppDbContext>(_ => new TestDbContext(dbOptions));
        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<AppDbContext>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        Db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Users = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var roles = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        foreach (var role in Enum.GetNames<UserRole>())
            roles.CreateAsync(new IdentityRole<int>(role)).GetAwaiter().GetResult();
    }

    public async Task<AppUser> CreateUserAsync(string email = "user@test.com", UserRole role = UserRole.Consumer,
                                               string password = "Passw0rd!")
    {
        var user = new AppUser { UserName = email, Email = email, FullName = "Test User" };
        var result = await Users.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        await Users.AddToRoleAsync(user, role.ToString());
        return user;
    }

    // یک محصول با موجودی مشخص + سبدی که ازش `quantity` عدد داخلشه
    public async Task<(Product Product, AppUser User)> CreateCartAsync(int stock, int quantity, decimal price = 2.5m)
    {
        var user = await CreateUserAsync();
        var category = new Category { Name = "Dairy" };
        var product = new Product { Name = "Milk", Price = price, StockQuantity = stock, Category = category };
        var cart = new Cart { UserId = user.Id };
        cart.Items.Add(new CartItem { Product = product, Quantity = quantity });
        Db.Carts.Add(cart);
        await Db.SaveChangesAsync();
        return (product, user);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
