using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Auth;
using Server.Data;
using Server.Entities;
using Shared.Obligations;

namespace Server.Controllers;

// CRUD обязательств
[ApiController]
[Authorize]
[Route("api/obligations")]
public sealed class ObligationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ObligationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<ObligationDto>>> GetAll(CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var items = await _db.Obligations
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ObligationDto(
                x.Id, x.Counterparty, x.Amount, x.Currency, (ObligationType)x.Type,
                x.CreatedAt, x.DueDate, x.IsPaid, x.PaidAt, x.Note))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ObligationDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var item = await _db.Obligations
            .Where(x => x.UserId == userId && !x.IsDeleted && x.Id == id)
            .Select(x => new ObligationDto(
                x.Id, x.Counterparty, x.Amount, x.Currency, (ObligationType)x.Type,
                x.CreatedAt, x.DueDate, x.IsPaid, x.PaidAt, x.Note))
            .SingleOrDefaultAsync(ct);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ObligationDto>> Create(CreateObligationRequest req, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var counterparty = (req.Counterparty ?? "").Trim();
        if (counterparty.Length is < 1 or > 200)
            return BadRequest("Counterparty length must be 1..200.");

        var currency = (req.Currency ?? "").Trim().ToUpperInvariant();
        if (currency.Length != 3)
            return BadRequest("Currency must be a 3-letter ISO code.");

        if (!Enum.IsDefined(typeof(ObligationType), req.Type))
            return BadRequest("Invalid obligation type.");

        if (req.Amount <= 0)
            return BadRequest("Amount must be greater than 0.");

        var entity = new ObligationEntity
        {
            UserId = userId,
            Counterparty = counterparty,
            Amount = req.Amount,
            Currency = currency,
            Type = (int)req.Type,
            DueDate = req.DueDate,
            Note = req.Note?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Obligations.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dto = new ObligationDto(
            entity.Id, entity.Counterparty, entity.Amount, entity.Currency, (ObligationType)entity.Type,
            entity.CreatedAt, entity.DueDate, entity.IsPaid, entity.PaidAt, entity.Note);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ObligationDto>> Update(Guid id, UpdateObligationRequest req, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var entity = await _db.Obligations.SingleOrDefaultAsync(
            x => x.UserId == userId && !x.IsDeleted && x.Id == id, ct);

        if (entity is null) return NotFound();

        var counterparty = (req.Counterparty ?? "").Trim();
        if (counterparty.Length is < 1 or > 200)
            return BadRequest("Counterparty length must be 1..200.");

        var currency = (req.Currency ?? "").Trim().ToUpperInvariant();
        if (currency.Length != 3)
            return BadRequest("Currency must be a 3-letter ISO code.");

        if (!Enum.IsDefined(typeof(ObligationType), req.Type))
            return BadRequest("Invalid obligation type.");

        entity.Counterparty = counterparty;
        entity.Amount = req.Amount;
        entity.Currency = currency;
        entity.Type = (int)req.Type;
        entity.DueDate = req.DueDate;
        entity.Note = req.Note?.Trim();
        
        if (req.IsPaid && !entity.IsPaid)
        {
            entity.IsPaid = true;
            entity.PaidAt = DateTimeOffset.UtcNow;
        }
        else if (!req.IsPaid && entity.IsPaid)
        {
            entity.IsPaid = false;
            entity.PaidAt = null;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new ObligationDto(
            entity.Id, entity.Counterparty, entity.Amount, entity.Currency, (ObligationType)entity.Type,
            entity.CreatedAt, entity.DueDate, entity.IsPaid, entity.PaidAt, entity.Note));
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> MarkAsPaid(Guid id, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var entity = await _db.Obligations.SingleOrDefaultAsync(
            x => x.UserId == userId && !x.IsDeleted && x.Id == id, ct);

        if (entity is null) return NotFound();

        if (entity.IsPaid)
            return BadRequest("Obligation is already paid.");

        if (!entity.IsPaid)
        {
            entity.IsPaid = true;
            entity.PaidAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var entity = await _db.Obligations.SingleOrDefaultAsync(
            x => x.UserId == userId && !x.IsDeleted && x.Id == id, ct);

        if (entity is null) return NotFound();

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}