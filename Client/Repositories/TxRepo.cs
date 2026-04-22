using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Data;
using Client.Models;
using Microsoft.Data.Sqlite;

namespace Client.Repositories;

public sealed class TransactionsRepository
{
    private readonly SqliteConFactory _factory;

    public event Action? Changed;
    private void RaiseChanged() => Changed?.Invoke();

    private readonly List<Transaction> _transactions = new();
    public IReadOnlyList<Transaction> All => _transactions;

    public TransactionsRepository(SqliteConFactory f) => _factory = f;

    public void Load()
    {
        _transactions.Clear();

        using var conn = _factory.Open();

        var txMap = new Dictionary<Guid, Transaction>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM Transactions ORDER BY Date DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var tx = new Transaction
                {
                    Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
                    Date = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("Date"))),
                    Description = r.GetString(r.GetOrdinal("Description"))
                };
                _transactions.Add(tx);
                txMap[tx.Id] = tx;
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM Entries";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var txId = Guid.Parse(r.GetString(r.GetOrdinal("TransactionId")));
                if (!txMap.TryGetValue(txId, out var tx)) continue;

                tx.Entries.Add(new Entry
                {
                    Id        = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
                    AccountId = Guid.Parse(r.GetString(r.GetOrdinal("AccountId"))),
                    CategoryId = r.IsDBNull(r.GetOrdinal("CategoryId"))
                        ? null
                        : Guid.Parse(r.GetString(r.GetOrdinal("CategoryId"))),
                    Direction = (EntryDirection)r.GetInt32(r.GetOrdinal("Direction")),
                    Amount    = new Money(
                        (decimal)r.GetDouble(r.GetOrdinal("Amount")),
                        r.GetString(r.GetOrdinal("CurrencyCode")))
                });
            }
        }
    }

    public int GetLocalCount() => _transactions.Count;

    // Добавляет транзакцию в кэш после успешного сохранения в БД
    public void AddToCache(Transaction tx)
    {
        _transactions.Insert(0, tx);
        RaiseChanged();
    }

    // Строит сторно-транзакцию (не сохраняет — это делает LocalDbService)
    public Transaction BuildStorno(Guid transactionId, IReadOnlyList<Account> accounts)
    {
        var tx = _transactions.FirstOrDefault(t => t.Id == transactionId)
            ?? throw new InvalidOperationException("Транзакция не найдена.");

        foreach (var e in tx.Entries)
        {
            var acc = accounts.FirstOrDefault(a => a.Id == e.AccountId);
            if (acc != null && acc.Type == AccountType.Assets && acc.IsDeleted)
                throw new InvalidOperationException(
                    "Нельзя сторнировать транзакцию: один из связанных счетов удален.");
        }

        var storno = new Transaction
        {
            Id          = Guid.NewGuid(),
            Date        = DateTimeOffset.Now,
            Description = $"[СТОРНО] {tx.Description}".Trim(),
            Entries     = new List<Entry>()
        };

        foreach (var e in tx.Entries)
        {
            storno.Entries.Add(new Entry
            {
                Id         = Guid.NewGuid(),
                AccountId  = e.AccountId,
                CategoryId = e.CategoryId,
                Direction  = e.Direction == EntryDirection.Debit
                    ? EntryDirection.Credit
                    : EntryDirection.Debit,
                Amount = new Money(e.Amount.Amount, e.Amount.CurrencyCode)
            });
        }

        return storno;
    }
}
