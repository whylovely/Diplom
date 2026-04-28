using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Controllers;

/// <summary>
/// Управление пользователями для роли Admin. Защищён политикой "Admin" — обычный пользователь
/// получит 403 даже с валидным JWT. Позволяет смотреть список юзеров, блокировать
/// и удалять обычных пользователей. Удалить или заблокировать другого админа нельзя — 400.
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminController(AppDbContext db) => _db = db;

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.Role,
                u.IsBlocked,
                u.CreatedAt,
                AccountsCount = _db.Accounts.Count(a => a.UserId == u.Id && !a.IsDeleted),
                TransactionsCount = _db.Transactions.Count(t => t.UserId == u.Id)
            })
            .ToListAsync(ct);

        return Ok(users);
    }

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.Role,
                u.IsBlocked,
                u.CreatedAt,
                AccountsCount = _db.Accounts.Count(a => a.UserId == u.Id && !a.IsDeleted),
                CategoriesCount = _db.Categories.Count(c => c.UserId == u.Id && !c.IsDeleted),
                TransactionsCount = _db.Transactions.Count(t => t.UserId == u.Id),
                ObligationsCount = _db.Obligations.Count(o => o.UserId == u.Id)
            })
            .SingleOrDefaultAsync(ct);

        return user is null ? NotFound() : Ok(user);
    }

    [HttpPut("users/{id:guid}/block")]
    public async Task<IActionResult> Block(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return NotFound();
        if (user.Role == "Admin") return BadRequest("Cannot block an admin.");

        user.IsBlocked = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("users/{id:guid}/unblock")]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return NotFound();

        user.IsBlocked = false;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return NotFound();
        if (user.Role == "Admin") return BadRequest("Cannot delete an admin.");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}