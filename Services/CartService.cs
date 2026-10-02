using Microsoft.EntityFrameworkCore;

public interface ICartService
{
    Task<CartDto> GetAsync(int userId);
    Task<(CartDto? Cart, string? Error)> AddItemAsync(int userId, CartItemAddDto dto);
    Task<(bool Found, string? Error)> UpdateItemAsync(int userId, int itemId, int quantity);
    Task<bool> RemoveItemAsync(int userId, int itemId);
    Task<bool> ClearAsync(int userId);
}

public class CartService : ICartService
{
    private readonly AppDbContext _db;
    public CartService(AppDbContext db) => _db = db;

    private Task<Cart?> LoadCart(int userId) =>
        _db.Carts.Include(c => c.Items).ThenInclude(i => i.Product)
                 .FirstOrDefaultAsync(c => c.UserId == userId);

    private static CartDto ToDto(Cart c)
    {
        var items = c.Items.Select(i => new CartItemDto(
            i.Id, i.ProductId, i.Product.Name, i.Product.Price, i.Quantity, i.Product.Price * i.Quantity)).ToList();
        return new CartDto(c.Id, items, items.Sum(i => i.LineTotal));
    }

    public async Task<CartDto> GetAsync(int userId)
    {
        var cart = await LoadCart(userId);
        return cart is null ? new CartDto(0, new List<CartItemDto>(), 0m) : ToDto(cart);
    }

    public async Task<(CartDto? Cart, string? Error)> AddItemAsync(int userId, CartItemAddDto dto)
    {
        var product = await _db.Products.FindAsync(dto.ProductId);
        if (product is null) return (null, "Product does not exist.");

        var cart = await LoadCart(userId);
        if (cart is null)
        {
            cart = new Cart { UserId = userId };
            _db.Carts.Add(cart);
        }

        var existing = cart.Items.FirstOrDefault(i => i.ProductId == dto.ProductId);
        var newQty = (existing?.Quantity ?? 0) + dto.Quantity;
        if (newQty > product.StockQuantity)
            return (null, $"Only {product.StockQuantity} in stock.");

        if (existing is null)
            cart.Items.Add(new CartItem { ProductId = dto.ProductId, Quantity = dto.Quantity });
        else
            existing.Quantity = newQty;

        await _db.SaveChangesAsync();
        return (ToDto((await LoadCart(userId))!), null);
    }

    public async Task<(bool Found, string? Error)> UpdateItemAsync(int userId, int itemId, int quantity)
    {
        // Filtering by Cart.UserId guarantees users can only touch THEIR OWN items
        var item = await _db.CartItems.Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.Cart.UserId == userId);
        if (item is null) return (false, null);
        if (quantity > item.Product.StockQuantity)
            return (true, $"Only {item.Product.StockQuantity} in stock.");

        item.Quantity = quantity;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> RemoveItemAsync(int userId, int itemId)
    {
        var item = await _db.CartItems.FirstOrDefaultAsync(i => i.Id == itemId && i.Cart.UserId == userId);
        if (item is null) return false;
        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ClearAsync(int userId)
    {
        var cart = await _db.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart is null) return false;
        _db.Carts.Remove(cart);   // items are removed by cascade
        await _db.SaveChangesAsync();
        return true;
    }
}
