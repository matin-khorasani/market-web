namespace Market.Tests;

public class CategoryServiceTests
{
    [Fact]
    public async Task Update_InvalidatesCachedProductsOfThatCategory()
    {
        using var env = new TestEnv();
        var category = new Category { Name = "Dairy" };
        var product = new Product { Name = "Milk", Price = 1m, StockQuantity = 5, Category = category };
        env.Db.Products.Add(product);
        await env.Db.SaveChangesAsync();
        var service = new CategoryService(env.Db, env.Cache);

        var updated = await service.UpdateAsync(category.Id, new CategorySaveDto { Name = "Milk & Cheese" });

        Assert.True(updated);
        Assert.Contains(ProductService.AllKey, env.Cache.Removed);
        Assert.Contains(ProductService.ItemKey(product.Id), env.Cache.Removed);
    }

    [Fact]
    public async Task Update_UnknownCategory_ReturnsFalse()
    {
        using var env = new TestEnv();
        var service = new CategoryService(env.Db, env.Cache);

        Assert.False(await service.UpdateAsync(999, new CategorySaveDto { Name = "X" }));
    }

    [Fact]
    public async Task Delete_CategoryWithProducts_ReturnsError()
    {
        using var env = new TestEnv();
        var category = new Category { Name = "Dairy" };
        env.Db.Products.Add(new Product { Name = "Milk", Price = 1m, StockQuantity = 5, Category = category });
        await env.Db.SaveChangesAsync();
        var service = new CategoryService(env.Db, env.Cache);

        var (found, error) = await service.DeleteAsync(category.Id);

        Assert.True(found);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Delete_EmptyCategory_Succeeds()
    {
        using var env = new TestEnv();
        var category = new Category { Name = "Empty" };
        env.Db.Categories.Add(category);
        await env.Db.SaveChangesAsync();
        var service = new CategoryService(env.Db, env.Cache);

        var (found, error) = await service.DeleteAsync(category.Id);

        Assert.True(found);
        Assert.Null(error);
    }
}
