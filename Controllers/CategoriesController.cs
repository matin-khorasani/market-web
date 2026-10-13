using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;
    public CategoriesController(ICategoryService categories) => _categories = categories;

    // Public
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll() =>
        await _categories.GetAllAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Get(int id)
    {
        var c = await _categories.GetAsync(id);
        return c is null ? NotFound() : c;
    }

    // Admin only
    [HttpPost, Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<CategoryDto>> Create(CategorySaveDto dto)
    {
        var c = await _categories.CreateAsync(dto);
        return CreatedAtAction(nameof(Get), new { id = c.Id }, c);
    }

    [HttpPut("{id:int}"), Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Update(int id, CategorySaveDto dto) =>
        await _categories.UpdateAsync(id, dto) ? NoContent() : NotFound();

    [HttpDelete("{id:int}"), Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Delete(int id)
    {
        var (found, error) = await _categories.DeleteAsync(id);
        if (!found) return NotFound();
        if (error is not null) return Problem(statusCode: 409, title: error);
        return NoContent();
    }
}
