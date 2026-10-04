using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync(int? categoryId, string? search);
    Task<ProductDto?> GetAsync(int id);
    Task<(ProductDto? Product, string? Error)> CreateAsync(ProductSaveDto dto);
    Task<(bool Found, string? Error)> UpdateAsync(int id, ProductSaveDto dto);
    Task<(bool Found, string? Error)> DeleteAsync(int id);
}

public class ProductService : IProductService
{
    private const string AllKey = "products:all";
    private static string ItemKey(int id) => $"product:{id}";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;       // injected by the DI container
    private readonly ICacheService _cache;

    public ProductService(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    // Select(...) projects straight to the DTO: SQL only fetches the columns we need
    private static readonly Expression<Func<Product, ProductDto>> ToDto = p => new ProductDto(
        p.Id, p.Name, p.Description, p.Price, p.StockQuantity,
        p.ImageUrl, p.CategoryId, p.Category.Name);

    public async Task<List<ProductDto>> GetAllAsync(int? categoryId, string? search)
    {
        // فقط لیست کامل (بدون فیلتر) کش می‌شه، چون ترکیب‌های جستجو بی‌نهایته
        var cacheable = !categoryId.HasValue && string.IsNullOrWhiteSpace(search);
        if (cacheable)
        {
            var cached = await _cache.GetAsync<List<ProductDto>>(AllKey);
            if (cached is not null) return cached;
        }

        var q = _db.Products.AsNoTracking().AsQueryable();
        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(p => p.Name.Contains(search));

        var list = await q.OrderBy(p => p.Name).Select(ToDto).ToListAsync();

        if (cacheable) await _cache.SetAsync(AllKey, list, Ttl);
        return list;
    }

    public async Task<ProductDto?> GetAsync(int id)
    {
        var cached = await _cache.GetAsync<ProductDto>(ItemKey(id));
        if (cached is not null) return cached;

        var dto = await _db.Products.AsNoTracking()
            .Where(p => p.Id == id)      // 1) اول فیلتر روی جدول اصلی
            .Select(ToDto)               // 2) بعد تبدیل به DTO
            .FirstOrDefaultAsync();

        if (dto is not null) await _cache.SetAsync(ItemKey(id), dto, Ttl);
        return dto;
    }

    public async Task<(ProductDto? Product, string? Error)> CreateAsync(ProductSaveDto dto)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId))
            return (null, "Category does not exist.");

        var p = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            ImageUrl = dto.ImageUrl,
            CategoryId = dto.CategoryId
        };
        _db.Products.Add(p);
        await _db.SaveChangesAsync();

        await _cache.RemoveAsync(AllKey);   // لیست کامل دیگه قدیمیه
        return (await GetAsync(p.Id), null);
    }

    public async Task<(bool Found, string? Error)> UpdateAsync(int id, ProductSaveDto dto)
    {
        var p = await _db.Products.FindAsync(id);
        if (p is null) return (false, null);
        if (!await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId))
            return (true, "Category does not exist.");

        p.Name = dto.Name; p.Description = dto.Description; p.Price = dto.Price;
        p.StockQuantity = dto.StockQuantity; p.ImageUrl = dto.ImageUrl; p.CategoryId = dto.CategoryId;
        await _db.SaveChangesAsync();

        await _cache.RemoveAsync(AllKey, ItemKey(id));
        return (true, null);
    }

    public async Task<(bool Found, string? Error)> DeleteAsync(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p is null) return (false, null);
        if (await _db.CartItems.AnyAsync(i => i.ProductId == id))
            return (true, "Product is in one or more carts.");

        _db.Products.Remove(p);
        await _db.SaveChangesAsync();

        await _cache.RemoveAsync(AllKey, ItemKey(id));
        return (true, null);
    }
}