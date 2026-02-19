using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Auth;
using Server.Data;
using Shared.Accounts;
using Shared.Reports;

namespace Server.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReportController(AppDbContext db) => _db = db;

    // Сводный отчёт: общий доход, расход, нетто + группировка по категориям.
    [HttpGet("summary")]
    public async Task<ActionResult<SummaryDto>> GetSummary(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var expenseByCategory = await GetCategoryTotals(userId, (int)AccountKind.Expenses, from, to, ct);
        var incomeByCategory  = await GetCategoryTotals(userId, (int)AccountKind.Income,   from, to, ct);

        var totalExpense = expenseByCategory.Sum(c => c.Total);
        var totalIncome  = incomeByCategory.Sum(c => c.Total);

        return Ok(new SummaryDto(totalIncome, totalExpense, totalIncome - totalExpense,
            expenseByCategory, incomeByCategory));
    }

    // Группировка по категориям для указанного вида счёта (1 = доход, 2 = расход).
    [HttpGet("by-category")]
    public async Task<ActionResult<IReadOnlyList<CategoryTotalDto>>> GetByCategory(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] int kind,
        CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        if (kind is not ((int)AccountKind.Income or (int)AccountKind.Expenses))
            return BadRequest("kind must be 1 (Income) or 2 (Expenses).");

        var result = await GetCategoryTotals(userId, kind, from, to, ct);
        return Ok(result);
    }

    // Помесячная динамика доходов и расходов.
    [HttpGet("monthly")]
    public async Task<ActionResult<IReadOnlyList<MonthlyTotalDto>>> GetMonthly(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var entries = await _db.Entries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Where(e => e.Transaction.Date >= from && e.Transaction.Date <= to)
            .Where(e => e.Direction == (int)Entities.EntryDirection.Debit)
            .Select(e => new
            {
                e.Transaction.Date,
                e.Account.Kind,
                e.Amount
            })
            .ToListAsync(ct);

        var monthly = entries
            .GroupBy(e => new { e.Date.Year, e.Date.Month })
            .Select(g => new MonthlyTotalDto(
                g.Key.Year,
                g.Key.Month,
                g.Where(x => x.Kind == (int)AccountKind.Income).Sum(x => x.Amount),
                g.Where(x => x.Kind == (int)AccountKind.Expenses).Sum(x => x.Amount)))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        return Ok(monthly);
    }

    // Обороты по счетам: дебет и кредит за период.
    [HttpGet("turnover")]
    public async Task<ActionResult<IReadOnlyList<AccountTurnoverDto>>> GetTurnover(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        var rows = await _db.Entries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Where(e => e.Transaction.Date >= from && e.Transaction.Date <= to)
            .GroupBy(e => new { e.AccountId, e.Account.Name, e.Account.Currency })
            .Select(g => new AccountTurnoverDto(
                g.Key.AccountId,
                g.Key.Name,
                g.Key.Currency,
                g.Where(x => x.Direction == (int)Entities.EntryDirection.Debit).Sum(x => x.Amount),
                g.Where(x => x.Direction == (int)Entities.EntryDirection.Credit).Sum(x => x.Amount)))
            .ToListAsync(ct);

        return Ok(rows);
    }

    private async Task<IReadOnlyList<CategoryTotalDto>> GetCategoryTotals(
        Guid userId, int accountKind, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var rows = await _db.Entries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Where(e => e.Account.Kind == accountKind)
            .Where(e => e.Direction == (int)Entities.EntryDirection.Debit)
            .Where(e => e.Transaction.Date >= from && e.Transaction.Date <= to)
            .GroupBy(e => new { e.CategoryId, CategoryName = e.Category!.Name })
            .Select(g => new CategoryTotalDto(
                g.Key.CategoryId,
                g.Key.CategoryName ?? "—",
                g.Sum(x => x.Amount)))
            .OrderByDescending(r => r.Total)
            .ToListAsync(ct);

        return rows;
    }
}