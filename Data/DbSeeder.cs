using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var userManager = sp.GetRequiredService<UserManager<AppUser>>();

        await db.Database.MigrateAsync();   // applies pending migrations (fine for development)

        // 1) Roles from the enum: "Consumer", "Admin"
        foreach (var role in Enum.GetNames<UserRole>())
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<int>(role));

        // 2) First admin (credentials come from appsettings -> "Seed")
        var adminEmail = config["Seed:AdminEmail"] ?? "admin@market.local";
        var adminPassword = config["Seed:AdminPassword"] ?? "Admin@12345";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new AppUser { UserName = adminEmail, Email = adminEmail, FullName = "Store Admin", EmailConfirmed = true };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, nameof(UserRole.Admin));
        }

        // 3) Sample catalog
        if (!await db.Categories.AnyAsync())
        {
            var dairy = new Category { Name = "Dairy", Description = "Milk, cheese, yogurt" };
            var bakery = new Category { Name = "Bakery", Description = "Fresh bread and pastries" };
            var drinks = new Category { Name = "Drinks", Description = "Juice, water, soda" };

            db.Products.AddRange(
                new Product { Name = "Milk 1L", Price = 1.20m, StockQuantity = 100, Category = dairy },
                new Product { Name = "Yogurt 500g", Price = 1.80m, StockQuantity = 60, Category = dairy },
                new Product { Name = "Toast Bread", Price = 2.10m, StockQuantity = 40, Category = bakery },
                new Product { Name = "Orange Juice 1L", Price = 2.90m, StockQuantity = 5, Category = drinks });

            await db.SaveChangesAsync();
        }
    }
}
