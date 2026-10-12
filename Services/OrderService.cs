using Microsoft.EntityFrameworkCore;

public interface IOrderService
{
    Task<(OrderDto? Order, string? Error)> CheckoutAsync(int userId);
    Task<List<OrderDto>> GetMineAsync(int userId);
    Task<(bool Found, OrderDto? Order, string? Error)> UpdateStatusAsync(int orderId, OrderStatus newStatus);
}

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;

    public OrderService(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    // از کدوم وضعیت به کدوم وضعیت‌ها می‌شه رفت
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Paid, OrderStatus.Cancelled },
        [OrderStatus.Paid] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
    };

    public async Task<(OrderDto? Order, string? Error)> CheckoutAsync(int userId)
    {
        var cart = await _db.Carts
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart is null || cart.Items.Count == 0)
            return (null, "Cart is empty.");

        foreach (var i in cart.Items)
            if (i.Quantity > i.Product.StockQuantity)
                return (null, $"Only {i.Product.StockQuantity} of '{i.Product.Name}' in stock.");

        var order = new Order { UserId = userId };
        foreach (var i in cart.Items)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.Product.Name,
                UnitPrice = i.Product.Price,
                Quantity = i.Quantity
            });
            i.Product.StockQuantity -= i.Quantity;
        }
        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        _db.Orders.Add(order);
        _db.Carts.Remove(cart);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (null, "Stock changed while processing your order. Please try again.");
        }

        await InvalidateProductCacheAsync(order.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value));
        return (ToDto(order), null);
    }

    public async Task<List<OrderDto>> GetMineAsync(int userId)
    {
        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)          // فقط سفارش‌های خود کاربر
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

        return orders.Select(ToDto).ToList();
    }

    public async Task<(bool Found, OrderDto? Order, string? Error)> UpdateStatusAsync(int orderId, OrderStatus newStatus)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return (false, null, null);

        if (!AllowedTransitions[order.Status].Contains(newStatus))
            return (true, null, $"Cannot change status from {order.Status} to {newStatus}.");

        var restoredProductIds = new List<int>();

        // کنسل = برگردوندن موجودی
        if (newStatus == OrderStatus.Cancelled)
        {
            restoredProductIds = order.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).ToList();
            var products = await _db.Products
                .Where(p => restoredProductIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var item in order.Items)
                if (item.ProductId is int pid && products.TryGetValue(pid, out var product))
                    product.StockQuantity += item.Quantity;
        }

        order.Status = newStatus;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (true, null, "The order or its stock was changed by someone else. Please try again.");
        }

        await InvalidateProductCacheAsync(restoredProductIds);
        return (true, ToDto(order), null);
    }

    // موجودی عوض شده، پس کش محصول‌ها کهنه‌ست
    private async Task InvalidateProductCacheAsync(IEnumerable<int> productIds)
    {
        var ids = productIds.ToList();
        if (ids.Count == 0) return;

        var keys = ids.Select(ProductService.ItemKey).Append(ProductService.AllKey).ToArray();
        await _cache.RemoveAsync(keys);
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.CreatedAtUtc, o.Status, o.Total,
        o.Items.Select(i => new OrderItemDto(
            i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity)).ToList());
}