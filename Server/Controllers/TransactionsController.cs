using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Auth;
using Server.Data;
using Server.Entities;
using Shared.Transactions;
using EntryDirection = Shared.Transactions.EntryDirection;

namespace Server.Controllers;

/// <summary>
/// CRUD транзакций с валидацией двойной записи. Каждая транзакция должна содержать
/// от 2 до 50 проводок, все по счетам одной валюты, и суммарно сбалансированной
/// (сумма Debit = сумме Credit). Принадлежность счетов и категорий пользователю
/// проверяется до сохранения, чтобы нельзя было сослаться на чужой ресурс.
/// </summary>
[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public TransactionsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<TransactionDto>>> GetAll(CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var items = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date)
            .Select(t => new TransactionDto(
                t.Id,
                t.Date,
                t.Description,
                t.Entries.Select(e => new EntryDto(
                    e.Id,
                    e.AccountId,
                    e.CategoryId,
                    (EntryDirection)e.Direction,
                    new MoneyDto(e.Amount, e.Currency)
                )).ToList()
            ))
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>
    /// Создаёт транзакцию двойной записи. Валидация в порядке стоимости проверки
    /// (от дешёвых in-memory к запросам к БД и в самом конце — баланс сумм).
    /// Запись делается в SQL-транзакции, чтобы либо сохранилась вся, либо ничего.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create(CreateTransactionRequest req, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        // Двойная запись требует минимум 2 проводки; верхний лимит — защита от случайного DoS
        if (req.Entries is null || req.Entries.Count < 2)
            return BadRequest("Transaction must contain at least 2 entries.");

        if (req.Entries.Count > 50)
            return BadRequest("Too many entries.");

        var accountIds = req.Entries.Select(e => e.AccountId).Distinct().ToList();
        var categoryIds = req.Entries.Where(e => e.CategoryId.HasValue).Select(e => e.CategoryId!.Value).Distinct().ToList();

        var accounts = await _db.Accounts
            .Where(a => a.UserId == userId && !a.IsDeleted && accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Currency })
            .ToListAsync(ct);

        if (accounts.Count != accountIds.Count)
            return BadRequest("One or more accounts are invalid or not owned by the user.");

        if (categoryIds.Count > 0)
        {
            var categoriesCount = await _db.Categories
                .Where(c => c.UserId == userId && !c.IsDeleted && categoryIds.Contains(c.Id))
                .CountAsync(ct);

            if (categoriesCount != categoryIds.Count)
                return BadRequest("One or more categories are invalid or not owned by the user.");
        }

        // Сейчас сервер не поддерживает мультивалютные транзакции — все проводки в одной валюте.
        // Конвертация валют делается через отдельный счёт-конвертер на клиенте.
        var distinctAccCurrencies = accounts.Select(a => a.Currency).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinctAccCurrencies.Count != 1)
            return BadRequest("All accounts in a transaction must have the same currency.");

        var accCurrency = distinctAccCurrencies[0];

        foreach (var e in req.Entries)
        {
            if (e.Money is null) return BadRequest("Entry money is missing.");
            var cur = (e.Money.Currency ?? "").Trim().ToUpperInvariant();
            if (cur.Length != 3) return BadRequest("Entry currency must be ISO 4217 (3 letters).");
            if (!string.Equals(cur, accCurrency, StringComparison.OrdinalIgnoreCase))
                return BadRequest("Entry currency must match account currency.");
            if (e.Money.Amount <= 0) return BadRequest("Entry amount must be > 0.");
            if (!Enum.IsDefined(typeof(EntryDirection), e.Direction))
                return BadRequest("Invalid entry direction.");
        }

        // Главная инвариантность двойной записи: Debit − Credit = 0.
        // Без этой проверки можно «нарисовать» деньги из ниоткуда.
        decimal signedSum = 0m;
        foreach (var e in req.Entries)
        {
            signedSum += e.Direction == EntryDirection.Debit ? e.Money!.Amount : -e.Money!.Amount;
        }

        if (signedSum != 0m)
            return BadRequest("Entries are not balanced (sum must be 0).");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var tr = new TransactionEntity
        {
            UserId = userId,
            Date = req.Date,
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim()
        };

        _db.Transactions.Add(tr);

        foreach (var e in req.Entries)
        {
            tr.Entries.Add(new EntryEntity
            {
                UserId = userId,
                AccountId = e.AccountId,
                CategoryId = e.CategoryId,
                Direction = (int)e.Direction,
                Amount = e.Money!.Amount,
                Currency = accCurrency
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var dto = new TransactionDto(
            tr.Id,
            tr.Date,
            tr.Description,
            tr.Entries.Select(en => new EntryDto(
                en.Id,
                en.AccountId,
                en.CategoryId,
                (EntryDirection)en.Direction,
                new MoneyDto(en.Amount, en.Currency)
            )).ToList()
        );

        return CreatedAtAction(nameof(GetById), new { id = tr.Id }, dto);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var tr = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Id == id)
            .Select(t => new TransactionDto(
                t.Id,
                t.Date,
                t.Description,
                t.Entries.Select(e => new EntryDto(
                    e.Id,
                    e.AccountId,
                    e.CategoryId,
                    (EntryDirection)e.Direction,
                    new MoneyDto(e.Amount, e.Currency)
                )).ToList()
            ))
            .SingleOrDefaultAsync(ct);

        return tr is null ? NotFound() : Ok(tr);
    }
}
