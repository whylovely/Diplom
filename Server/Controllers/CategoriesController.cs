using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.DTO;
using Server.Entities;

namespace Server.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IReadOnlyList<CategoryDto>> Get()
        => await _db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name))
            .ToListAsync();

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required");

        var exists = await _db.Categories.AnyAsync(c => c.Name.ToLower() == dto.Name.Trim().ToLower());
        if (exists) return Conflict("Category already exists");

        var entity = new CategoryEntity { Name = dto.Name.Trim() };
        _db.Categories.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new CategoryDto(entity.Id, entity.Name));
    }
}