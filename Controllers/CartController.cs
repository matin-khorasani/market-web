using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// The user id comes from the JWT, never from the URL -> nobody can read someone else's cart
[ApiController]
[Route("api/cart")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cart;
    public CartController(ICartService cart) => _cart = cart;

    [HttpGet]
    public async Task<ActionResult<CartDto>> Get() => await _cart.GetAsync(User.GetUserId());

    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem(CartItemAddDto dto)
    {
        var (cart, error) = await _cart.AddItemAsync(User.GetUserId(), dto);
        return error is not null ? BadRequest(error) : Ok(cart);
    }

    [HttpPut("items/{itemId:int}")]
    public async Task<IActionResult> UpdateItem(int itemId, CartItemUpdateDto dto)
    {
        var (found, error) = await _cart.UpdateItemAsync(User.GetUserId(), itemId, dto.Quantity);
        if (!found) return NotFound();
        return error is not null ? BadRequest(error) : NoContent();
    }

    [HttpDelete("items/{itemId:int}")]
    public async Task<IActionResult> RemoveItem(int itemId) =>
        await _cart.RemoveItemAsync(User.GetUserId(), itemId) ? NoContent() : NotFound();

    [HttpDelete]
    public async Task<IActionResult> Clear() =>
        await _cart.ClearAsync(User.GetUserId()) ? NoContent() : NotFound();
}
