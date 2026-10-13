using Microsoft.EntityFrameworkCore;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync();
    Task<CategoryDto?> GetAsync(int id);
    Task<CategoryDto> CreateAsync(CategorySaveDto dto);
    Task<bool> UpdateAsync(int id, CategorySaveDto dto);
    Task<(bool Found, string? Error)> DeleteAsync(int id);
}

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;

    public CategoryService(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public Task<List<CategoryDto>> GetAllAsync() =>
        _db.Categories.AsNoTracking()
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description))
            .ToListAsync();

    public Task<CategoryDto?> GetAsync(int id) =>
        _db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description))
            .FirstOrDefaultAsync();

    public async Task<CategoryDto> CreateAsync(CategorySaveDto dto)
    {
        var c = new Category { Name = dto.Name, Description = dto.Description };
        _db.Categories.Add(c);
        await _db.SaveChangesAsync();
        return new CategoryDto(c.Id, c.Name, c.Description);
    }

    public async Task<bool> UpdateAsync(int id, CategorySaveDto dto)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c is null) return false;

        c.Name = dto.Name;
        c.Description = dto.Description;
        await _db.SaveChangesAsync();

        // ProductDto اسم دسته‌بندی رو هم داره؛ پس cache محصولات این دسته و لیست کامل کهنه شدن
        var productIds = await _db.Products.Where(p => p.CategoryId == id).Select(p => p.Id).ToListAsync();
        var keys = productIds.Select(ProductService.ItemKey).Append(ProductService.AllKey).ToArray();
        await _cache.RemoveAsync(keys);
        return true;
    }

    public async Task<(bool Found, string? Error)> DeleteAsync(int id)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c is null) return (false, null);
        if (await _db.Products.AnyAsync(p => p.CategoryId == id))
            return (true, "Category still has products. Move or delete them first.");

        _db.Categories.Remove(c);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
