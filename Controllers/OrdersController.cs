using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    public OrdersController(IOrderService orders) => _orders = orders;

    [HttpPost("checkout")]
    public async Task<ActionResult<OrderDto>> Checkout()
    {
        var (order, error) = await _orders.CheckoutAsync(User.GetUserId());
        if (error is not null) return BadRequest(error);
        return Ok(order);
    }
    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> GetMine()
    => await _orders.GetMineAsync(User.GetUserId());

}