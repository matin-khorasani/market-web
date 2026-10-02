using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;
    public ProductsController(IProductService products) => _products = products;

    // Public
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetAll(int? categoryId, string? search) =>
        await _products.GetAllAsync(categoryId, search);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> Get(int id)
    {
        var p = await _products.GetAsync(id);
        return p is null ? NotFound() : p;
    }

    // Admin only
    [HttpPost, Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ProductDto>> Create(ProductSaveDto dto)
    {
        var (product, error) = await _products.CreateAsync(dto);
        return error is not null ? BadRequest(error)
            : CreatedAtAction(nameof(Get), new { id = product!.Id }, product);
    }

    [HttpPut("{id:int}"), Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Update(int id, ProductSaveDto dto)
    {
        var (found, error) = await _products.UpdateAsync(id, dto);
        if (!found) return NotFound();
        return error is not null ? BadRequest(error) : NoContent();
    }

    [HttpDelete("{id:int}"), Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Delete(int id)
    {
        var (found, error) = await _products.DeleteAsync(id);
        if (!found) return NotFound();
        return error is not null ? Conflict(error) : NoContent();
    }
}
