using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Auth;
using Server.Data;
using Server.Entities;
using Shared.Accounts;

namespace Server.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public sealed class AccountsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AccountsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAll(CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var items = await _db.Accounts
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new AccountDto(
                x.Id, x.Name, (AccountKind)x.Kind, x.Currency,
                (MultiCurrencyType)x.AccountType, x.SecondaryCurrency, x.ExchangeRate))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var item = await _db.Accounts
            .Where(x => x.UserId == userId && !x.IsDeleted && x.Id == id)
            .Select(x => new AccountDto(
                x.Id, x.Name, (AccountKind)x.Kind, x.Currency,
                (MultiCurrencyType)x.AccountType, x.SecondaryCurrency, x.ExchangeRate))
            .SingleOrDefaultAsync(ct);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create(CreateAccountRequest req, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var name = (req.Name ?? "").Trim();
        if (name.Length is < 1 or > 200)
            return BadRequest("Name length must be 1..200.");

        var currency = (req.Currency ?? "").Trim().ToUpperInvariant();
        if (currency.Length != 3)
            return BadRequest("Currency must be a 3-letter ISO code (e.g., PLN, USD, EUR).");

        if (!Enum.IsDefined(typeof(AccountKind), req.Kind))
            return BadRequest("Invalid account kind.");

        if (!Enum.IsDefined(typeof(MultiCurrencyType), req.AccountType))
            return BadRequest("Invalid account type.");

        string? secondaryCurrency = null;
        decimal? exchangeRate = null;

        if (req.AccountType == MultiCurrencyType.MultiCurrency)
        {
            secondaryCurrency = (req.SecondaryCurrency ?? "").Trim().ToUpperInvariant();
            if (secondaryCurrency.Length != 3)
                return BadRequest("SecondaryCurrency must be a 3-letter ISO code.");

            if (secondaryCurrency == currency)
                return BadRequest("SecondaryCurrency must differ from the primary Currency.");

            if (req.ExchangeRate is null or <= 0)
                return BadRequest("ExchangeRate must be greater than 0 for multi-currency accounts.");

            exchangeRate = req.ExchangeRate;
        }

        var exists = await _db.Accounts.AnyAsync(
            x => x.UserId == userId && !x.IsDeleted && x.Name == name, ct);

        if (exists) return Conflict("Account with the same name already exists.");

        var entity = new AccountEntity
        {
            UserId = userId,
            Name = name,
            Kind = (int)req.Kind,
            Currency = currency,
            AccountType = (int)req.AccountType,
            SecondaryCurrency = secondaryCurrency,
            ExchangeRate = exchangeRate
        };

        _db.Accounts.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dto = new AccountDto(
            entity.Id, entity.Name, (AccountKind)entity.Kind, entity.Currency,
            (MultiCurrencyType)entity.AccountType, entity.SecondaryCurrency, entity.ExchangeRate);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AccountDto>> Update(Guid id, UpdateAccountRequest req, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var entity = await _db.Accounts.SingleOrDefaultAsync(
            x => x.UserId == userId && !x.IsDeleted && x.Id == id, ct);

        if (entity is null) return NotFound();

        var name = (req.Name ?? "").Trim();
        if (name.Length is < 1 or > 200)
            return BadRequest("Name length must be 1..200.");

        var currency = (req.Currency ?? "").Trim().ToUpperInvariant();
        if (currency.Length != 3)
            return BadRequest("Currency must be a 3-letter ISO code (e.g., PLN, USD, EUR).");

        if (!Enum.IsDefined(typeof(AccountKind), req.Kind))
            return BadRequest("Invalid account kind.");

        if (!Enum.IsDefined(typeof(MultiCurrencyType), req.AccountType))
            return BadRequest("Invalid account type.");

        string? secondaryCurrency = null;
        decimal? exchangeRate = null;

        if (req.AccountType == MultiCurrencyType.MultiCurrency)
        {
            secondaryCurrency = (req.SecondaryCurrency ?? "").Trim().ToUpperInvariant();
            if (secondaryCurrency.Length != 3)
                return BadRequest("SecondaryCurrency must be a 3-letter ISO code.");

            if (secondaryCurrency == currency)
                return BadRequest("SecondaryCurrency must differ from the primary Currency.");

            if (req.ExchangeRate is null or <= 0)
                return BadRequest("ExchangeRate must be greater than 0 for multi-currency accounts.");

            exchangeRate = req.ExchangeRate;
        }

        var exists = await _db.Accounts.AnyAsync(
            x => x.UserId == userId && !x.IsDeleted && x.Name == name && x.Id != id, ct);

        if (exists) return Conflict("Account with the same name already exists.");

        entity.Name = name;
        entity.Kind = (int)req.Kind;
        entity.Currency = currency;
        entity.AccountType = (int)req.AccountType;
        entity.SecondaryCurrency = secondaryCurrency;
        entity.ExchangeRate = exchangeRate;

        await _db.SaveChangesAsync(ct);

        return Ok(new AccountDto(
            entity.Id, entity.Name, (AccountKind)entity.Kind, entity.Currency,
            (MultiCurrencyType)entity.AccountType, entity.SecondaryCurrency, entity.ExchangeRate));
    }

    // Soft delete
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var entity = await _db.Accounts.SingleOrDefaultAsync(
            x => x.UserId == userId && !x.IsDeleted && x.Id == id, ct);

        if (entity is null) return NotFound();

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}