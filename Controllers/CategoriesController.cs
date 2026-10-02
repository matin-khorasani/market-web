using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriesController(AppDbContext db) => _db = db;

    // Public
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll() =>
        await _db.Categories.AsNoTracking()
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description))
            .ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Get(int id)
    {
        var c = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return c is null ? NotFound() : new CategoryDto(c.Id, c.Name, c.Description);
    }

    // Admin only
    [HttpPost, Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<CategoryDto>> Create(CategorySaveDto dto)
    {
        var c = new Category { Name = dto.Name, Description = dto.Description };
        _db.Categories.Add(c);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = c.Id }, new CategoryDto(c.Id, c.Name, c.Description));
    }

    [HttpPut("{id:int}"), Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Update(int id, CategorySaveDto dto)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c is null) return NotFound();
        c.Name = dto.Name;
        c.Description = dto.Description;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}"), Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c is null) return NotFound();
        if (await _db.Products.AnyAsync(p => p.CategoryId == id))
            return Conflict("Category still has products. Move or delete them first.");
        _db.Categories.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
