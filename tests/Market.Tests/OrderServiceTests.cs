using Microsoft.EntityFrameworkCore;

namespace Market.Tests;

public class OrderServiceTests
{
    [Fact]
    public async Task Checkout_CreatesOrder_DecreasesStock_AndClearsCart()
    {
        using var env = new TestEnv();
        var (product, user) = await env.CreateCartAsync(stock: 10, quantity: 3, price: 2.5m);
        var service = new OrderService(env.Db, env.Cache);

        var (order, error) = await service.CheckoutAsync(user.Id);

        Assert.Null(error);
        Assert.NotNull(order);
        Assert.Equal(7.5m, order!.Total);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(7, (await env.Db.Products.SingleAsync(p => p.Id == product.Id)).StockQuantity);
        Assert.Empty(env.Db.Carts);
    }

    [Fact]
    public async Task Checkout_SavesPriceSnapshot_InOrderItem()
    {
        using var env = new TestEnv();
        var (product, user) = await env.CreateCartAsync(stock: 10, quantity: 2, price: 4m);
        var service = new OrderService(env.Db, env.Cache);
        await service.CheckoutAsync(user.Id);

        // قیمت بعد از خرید عوض می‌شه، ولی سفارش قدیمی نباید تغییر کنه
        product.Price = 99m;
        await env.Db.SaveChangesAsync();

        var orders = await service.GetMineAsync(user.Id);
        Assert.Equal(4m, orders.Single().Items.Single().UnitPrice);
    }

    [Fact]
    public async Task Checkout_EmptyCart_ReturnsError()
    {
        using var env = new TestEnv();
        var user = await env.CreateUserAsync();
        var service = new OrderService(env.Db, env.Cache);

        var (order, error) = await service.CheckoutAsync(user.Id);

        Assert.Null(order);
        Assert.Equal("Cart is empty.", error);
    }

    [Fact]
    public async Task Checkout_NotEnoughStock_ReturnsError_AndKeepsStock()
    {
        using var env = new TestEnv();
        var (product, user) = await env.CreateCartAsync(stock: 2, quantity: 5);
        var service = new OrderService(env.Db, env.Cache);

        var (order, error) = await service.CheckoutAsync(user.Id);

        Assert.Null(order);
        Assert.NotNull(error);
        Assert.Equal(2, (await env.Db.Products.SingleAsync(p => p.Id == product.Id)).StockQuantity);
        Assert.Empty(env.Db.Orders);
    }

    [Fact]
    public async Task Checkout_InvalidatesProductCache()
    {
        using var env = new TestEnv();
        var (product, user) = await env.CreateCartAsync(stock: 10, quantity: 1);
        var service = new OrderService(env.Db, env.Cache);

        await service.CheckoutAsync(user.Id);

        Assert.Contains(ProductService.AllKey, env.Cache.Removed);
        Assert.Contains(ProductService.ItemKey(product.Id), env.Cache.Removed);
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_ReturnsError()
    {
        using var env = new TestEnv();
        var user = await env.CreateUserAsync();
        var order = new Order { UserId = user.Id, Status = OrderStatus.Shipped };
        env.Db.Orders.Add(order);
        await env.Db.SaveChangesAsync();
        var service = new OrderService(env.Db, env.Cache);

        var (found, result, error) = await service.UpdateStatusAsync(order.Id, OrderStatus.Paid);

        Assert.True(found);
        Assert.Null(result);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task UpdateStatus_UnknownOrder_ReturnsNotFound()
    {
        using var env = new TestEnv();
        var service = new OrderService(env.Db, env.Cache);

        var (found, _, _) = await service.UpdateStatusAsync(12345, OrderStatus.Paid);

        Assert.False(found);
    }

    [Fact]
    public async Task Cancel_RestoresStock()
    {
        using var env = new TestEnv();
        var (product, user) = await env.CreateCartAsync(stock: 10, quantity: 4);
        var service = new OrderService(env.Db, env.Cache);
        var (order, _) = await service.CheckoutAsync(user.Id);
        Assert.Equal(6, (await env.Db.Products.SingleAsync(p => p.Id == product.Id)).StockQuantity);

        var (_, cancelled, error) = await service.UpdateStatusAsync(order!.Id, OrderStatus.Cancelled);

        Assert.Null(error);
        Assert.Equal(OrderStatus.Cancelled, cancelled!.Status);
        Assert.Equal(10, (await env.Db.Products.SingleAsync(p => p.Id == product.Id)).StockQuantity);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnOrders()
    {
        using var env = new TestEnv();
        var alice = await env.CreateUserAsync("alice@test.com");
        var bob = await env.CreateUserAsync("bob@test.com");
        env.Db.Orders.Add(new Order { UserId = alice.Id });
        env.Db.Orders.Add(new Order { UserId = bob.Id });
        await env.Db.SaveChangesAsync();
        var service = new OrderService(env.Db, env.Cache);

        var mine = await service.GetMineAsync(alice.Id);

        Assert.Single(mine);
    }
}
