using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Auth;
using Server.Data;
using Server.Entities;
using Shared.Categories;
using Shared.Accounts;
using Shared.Auth;

namespace Server.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll(CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var items = await _db.Categories
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new CategoryDto(x.Id, x.Name))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var item = await _db.Categories
            .Where(x => x.UserId == userId && !x.IsDeleted && x.Id == id)
            .Select(x => new CategoryDto(x.Id, x.Name))
            .SingleOrDefaultAsync(ct);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest req, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var name = (req.Name ?? "").Trim();
        if (name.Length is < 1 or > 200)
            return BadRequest("Name length must be 1..200.");

        var exists = await _db.Categories
            .AnyAsync(x => x.UserId == userId && !x.IsDeleted && x.Name == name, ct);

        if (exists) return Conflict("Category with the same name already exists.");

        var entity = new CategoryEntity
        {
            UserId = userId,
            Name = name
        };

        _db.Categories.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dto = new CategoryDto(entity.Id, entity.Name);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, UpdateCategoryRequest req, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var entity = await _db.Categories
            .SingleOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted && x.Id == id, ct);

        if (entity is null) return NotFound();

        var name = (req.Name ?? "").Trim();
        if (name.Length is < 1 or > 200)
            return BadRequest("Name length must be 1..200.");

        var exists = await _db.Categories.AnyAsync(
            x => x.UserId == userId && !x.IsDeleted && x.Name == name && x.Id != id, ct);

        if (exists) return Conflict("Category with the same name already exists.");

        entity.Name = name;
        await _db.SaveChangesAsync(ct);

        return Ok(new CategoryDto(entity.Id, entity.Name));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var entity = await _db.Categories
            .SingleOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted && x.Id == id, ct);

        if (entity is null) return NotFound();

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}