using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Data;
using Client.Models;
using Microsoft.Data.Sqlite;

namespace Client.Repositories;

/// <summary>
/// Репозиторий транзакций. В отличие от других репозиториев сам не сохраняет новые
/// транзакции — это делает <c>LocalDbService.PostTransactionAsync</c> (там нужна
/// SQL-транзакция по таблицам Transactions/Entries/Accounts одновременно).
///
/// Этот класс отвечает только за загрузку из БД, кеш в памяти и построение сторно.
/// Свежесозданные транзакции добавляются в кеш через <see cref="AddToCache"/>.
/// </summary>
public sealed class TransactionsRepository
{
    private readonly SqliteConFactory _factory;

    public event Action? Changed;
    private void RaiseChanged() => Changed?.Invoke();

    private readonly List<Transaction> _transactions = new();
    public IReadOnlyList<Transaction> All => _transactions;

    public TransactionsRepository(SqliteConFactory f) => _factory = f;

    /// <summary>
    /// Перечитывает все транзакции и проводки из БД. Делается двумя запросами
    /// (один по Transactions, второй по Entries) и сборкой в памяти —
    /// проще, чем JOIN с разворачиванием в иерархию.
    /// </summary>
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

    /// <summary>
    /// Добавляет уже сохранённую в БД транзакцию в начало кеша.
    /// Вызывается из LocalDbService после успешной записи — чтобы UI получил событие
    /// и список обновился без полного Load.
    /// </summary>
    public void AddToCache(Transaction tx)
    {
        _transactions.Insert(0, tx);
        RaiseChanged();
    }

    /// <summary>
    /// Строит сторно (обратную транзакцию) на основе существующей: те же проводки,
    /// но с инвертированными направлениями Debit↔Credit. Используется для отмены операции.
    /// Возвращает объект <see cref="Transaction"/>, не сохраняет его — это делает
    /// LocalDbService.StornoTransactionAsync через PostTransactionAsync.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Если транзакция не найдена или один из её Asset-счетов уже удалён.
    /// </exception>
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
