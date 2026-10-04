using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// IdentityDbContext adds the Users / Roles / UserRoles ... tables for us.
public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);   // IMPORTANT: configures the Identity tables

        modelBuilder.Entity<AppUser>()
            .HasOne(u => u.Cart)
            .WithOne(c => c.User)
            .HasForeignKey<Cart>(c => c.UserId);

        modelBuilder.Entity<Cart>()
            .HasMany(c => c.Items)
            .WithOne(i => i.Cart)
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        // A product can appear only once per cart (quantity handles the rest)
        modelBuilder.Entity<CartItem>()
            .HasIndex(i => new { i.CartId, i.ProductId })
            .IsUnique();

        modelBuilder.Entity<CartItem>()
            .HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);                  // حذف کاربر = حذف توکن‌هاش
            e.Property(t => t.TokenHash).HasMaxLength(64);       // هش SHA-256 به شکل hex = ۶۴ کاراکتر
            e.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
            e.HasIndex(t => t.TokenHash).IsUnique();             // جستجو با هش سریع و یکتا باشه
        });
    }
}
