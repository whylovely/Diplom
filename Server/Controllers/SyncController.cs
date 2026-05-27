using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Auth;
using Server.Data;
using Server.Entities;
using Shared.Sync;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Controllers;

[ApiController]
[Authorize]
[Route("api/sync")]
public sealed class SyncController : ControllerBase
{
    private readonly AppDbContext _db;

    public SyncController(AppDbContext db) => _db = db;

    [HttpPost("push")]
    public async Task<IActionResult> Push(SyncPushRequest req, CancellationToken ct)
    {
        var userId = UserContext.GetUserId(User);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await _db.Entries.Where(e => e.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.Transactions.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.Obligations.Where(o => o.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.Categories.Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.Accounts.Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);

        if (req.Accounts != null && req.Accounts.Count > 0)
        {
            var accEntities = req.Accounts.Select(a => new AccountEntity
            {
                Id = a.Id,
                UserId = userId,
                Name = a.Name,
                Kind = (int)a.Kind,
                Currency = a.Currency,
                AccountType = (int)a.AccountType,
                SecondaryCurrency = a.SecondaryCurrency,
                ExchangeRate = a.ExchangeRate,
                IsDeleted = false
            });
            await _db.Accounts.AddRangeAsync(accEntities, ct);
        }

        if (req.Categories != null && req.Categories.Count > 0)
        {
            var catEntities = req.Categories.Select(c => new CategoryEntity
            {
                Id = c.Id,
                UserId = userId,
                Name = c.Name,
                IsDeleted = false
            });
            await _db.Categories.AddRangeAsync(catEntities, ct);
        }

        if (req.Obligations != null && req.Obligations.Count > 0)
        {
            var oblEntities = req.Obligations.Select(o => new ObligationEntity
            {
                Id = o.Id,
                UserId = userId,
                Counterparty = o.Counterparty,
                Amount = o.Amount,
                Currency = o.Currency,
                Type = (int)o.Type,
                CreatedAt = o.CreatedAt.ToUniversalTime(),
                DueDate = o.DueDate?.ToUniversalTime(),
                IsPaid = o.IsPaid,
                PaidAt = o.PaidAt?.ToUniversalTime(),
                Note = o.Note,
                IsDeleted = false
            });
            await _db.Obligations.AddRangeAsync(oblEntities, ct);
        }

        if (req.Transactions != null && req.Transactions.Count > 0)
        {
            var trEntities = req.Transactions.Select(t => new TransactionEntity
            {
                Id = t.Id,
                UserId = userId,
                Date = t.Date.ToUniversalTime(),
                Description = t.Description,
                Entries = t.Entries.Select(e => new EntryEntity
                {
                    Id = e.Id,
                    UserId = userId,
                    AccountId = e.AccountId,
                    CategoryId = e.CategoryId,
                    Direction = (int)e.Direction,
                    Amount = e.Money.Amount,
                    Currency = e.Money.Currency
                }).ToList()
            });
            await _db.Transactions.AddRangeAsync(trEntities, ct);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine("DB SAVE EXCEPTION: " + ex.Message);
            if (ex.InnerException != null)
                Console.WriteLine("INNER: " + ex.InnerException.Message);
            throw;
        }

        return Ok();
    }
}