using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Types;
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
}