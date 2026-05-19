using System;
using System.Collections.Generic;
using System.Linq;
using Client.Models;

namespace Client.Services;

// Функции агрегации транзакций
public static class TransactionAggregator
{
    public static IReadOnlyList<Transaction> FilterByDateRange(
        IReadOnlyList<Transaction> transactions,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (!from.HasValue || !to.HasValue) return Array.Empty<Transaction>();
        return transactions
            .Where(t => t.Date.Date >= from.Value.Date && t.Date.Date <= to.Value.Date)
            .ToList();
    }

    private static HashSet<Guid> BuildStornoExclusionSet(IReadOnlyList<Transaction> txInRange)
    {
        const string prefix = "[СТОРНО] ";
        var excluded = new HashSet<Guid>();

        var stornoTxs = txInRange
            .Where(t => !string.IsNullOrEmpty(t.Description) && t.Description.StartsWith("[СТОРНО]"))
            .ToList();
        foreach (var t in stornoTxs) excluded.Add(t.Id);

        var originalDescriptions = stornoTxs
            .Where(t => t.Description.StartsWith(prefix))
            .Select(t => t.Description.Substring(prefix.Length))
            .ToHashSet();

        foreach (var t in txInRange)
            if (!string.IsNullOrEmpty(t.Description) && originalDescriptions.Contains(t.Description))
                excluded.Add(t.Id);

        return excluded;
    }

    public static IEnumerable<Entry> GetExpenseEntries(
        IReadOnlyList<Transaction> txInRange,
        IDictionary<Guid, Account> accountById)
    {
        var stornoExcluded = BuildStornoExclusionSet(txInRange);

        foreach (var tx in txInRange)
        {
            // Сторно и оригиналы сторно не идут в категорийный отчёт
            if (stornoExcluded.Contains(tx.Id)) continue;

            bool isTransfer = tx.Entries.Count >= 2 && tx.Entries.All(e =>
                accountById.TryGetValue(e.AccountId, out var a) && a.Type == AccountType.Assets);
            if (isTransfer) continue;

            var expenseEntry = tx.Entries.FirstOrDefault(e =>
                accountById.TryGetValue(e.AccountId, out var acc)
                && acc.Type == AccountType.Expense
                && e.Direction == EntryDirection.Debit);

            if (expenseEntry != null) { yield return expenseEntry; continue; }

            foreach (var e in tx.Entries)
                if (accountById.TryGetValue(e.AccountId, out var acc)
                    && acc.Type == AccountType.Assets
                    && e.Direction == EntryDirection.Credit
                    && e.CategoryId.HasValue)
                    yield return e;
        }
    }

    public static IEnumerable<Entry> GetIncomeEntries(
        IReadOnlyList<Transaction> txInRange,
        IDictionary<Guid, Account> accountById)
    {
        var stornoExcluded = BuildStornoExclusionSet(txInRange);

        foreach (var tx in txInRange)
        {
            if (stornoExcluded.Contains(tx.Id)) continue;

            bool isTransfer = tx.Entries.Count >= 2 && tx.Entries.All(e =>
                accountById.TryGetValue(e.AccountId, out var a) && a.Type == AccountType.Assets);
            if (isTransfer) continue;

            var incomeEntry = tx.Entries.FirstOrDefault(e =>
                accountById.TryGetValue(e.AccountId, out var acc)
                && acc.Type == AccountType.Income
                && e.Direction == EntryDirection.Credit);

            if (incomeEntry != null) { yield return incomeEntry; continue; }

            foreach (var e in tx.Entries)
                if (accountById.TryGetValue(e.AccountId, out var acc)
                    && acc.Type == AccountType.Assets
                    && e.Direction == EntryDirection.Debit
                    && e.CategoryId.HasValue)
                    yield return e;
        }
    }

    // Возвращает виртуальное название «категории» для проводок без CategoryId
    private static string ResolveCategoryName(
        Entry e,
        IReadOnlyList<Category> categories,
        IDictionary<Guid, Account>? accountById)
    {
        if (e.CategoryId.HasValue)
            return categories.FirstOrDefault(c => c.Id == e.CategoryId.Value)?.Name ?? "—";

        if (accountById != null && accountById.TryGetValue(e.AccountId, out var acc))
        {
            if (acc.Type == AccountType.Expense) return "Погашение долга";
            if (acc.Type == AccountType.Income)  return "Возврат долга";
        }
        return "—";
    }

    public static IReadOnlyList<CategoryShareRow> AggregateByCategoryRows(
        IEnumerable<Entry> entries,
        IReadOnlyList<Category> categories,
        Func<string, string, decimal> getRate,
        string baseCurrency,
        IDictionary<Guid, Account>? accountById = null)
    {
        return entries
            .GroupBy(e => ResolveCategoryName(e, categories, accountById))
            .Select(g => new CategoryShareRow
            {
                CategoryName = g.Key,
                Total = g.Sum(e => e.Amount.Amount * getRate(e.Amount.CurrencyCode, baseCurrency))
            })
            .OrderByDescending(r => r.Total)
            .ToList();
    }

    public static IReadOnlyList<CategoryDetailGroup> AggregateByCategoryGroups(
        IEnumerable<Entry> entries,
        IReadOnlyList<Transaction> txInRange,
        IReadOnlyList<Category> categories,
        Func<string, string, decimal> getRate,
        string baseCurrency,
        IDictionary<Guid, Account>? accountById = null)
    {
        var entryList = entries.ToList();
        var txById = txInRange
            .SelectMany(t => t.Entries.Select(e => new { e.Id, Tx = t }))
            .ToDictionary(x => x.Id, x => x.Tx);

        return entryList
            .Select(e => new { Entry = e, Tx = txById.GetValueOrDefault(e.Id) })
            .Where(x => x.Tx != null)
            .GroupBy(x => ResolveCategoryName(x.Entry, categories, accountById))
            .Select(g =>
            {
                var catName = g.Key;
                var days = g
                    .GroupBy(x => x.Tx!.Date.Date)
                    .OrderBy(d => d.Key)
                    .Select(d => new DailyDetailRow
                    {
                        Date = d.Key.ToString("dd.MM.yyyy"),
                        Amount = d.Sum(x => x.Entry.Amount.Amount * getRate(x.Entry.Amount.CurrencyCode, baseCurrency)),
                        Description = string.Join(", ",
                            d.Select(x => x.Tx!.Description)
                             .Where(s => !string.IsNullOrWhiteSpace(s))
                             .Distinct())
                    })
                    .ToList();

                return new CategoryDetailGroup
                {
                    CategoryName = catName,
                    Total = g.Sum(x => x.Entry.Amount.Amount * getRate(x.Entry.Amount.CurrencyCode, baseCurrency)),
                    Days = days
                };
            })
            .OrderByDescending(r => r.Total)
            .ToList();
    }

    public static IReadOnlyList<MonthlyTotalRow> BuildMonthlyTotals(
        IReadOnlyList<Transaction> transactions,
        IDictionary<Guid, Account> accountById,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Func<string, string, decimal> getRate,
        string baseCurrency)
    {
        var txInRange = FilterByDateRange(transactions, from, to);
        if (txInRange.Count == 0) return Array.Empty<MonthlyTotalRow>();

        return txInRange
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(mg =>
            {
                var txList = mg.ToList();

                var expense = txList.Sum(tx =>
                {
                    var expEntry = tx.Entries.FirstOrDefault(e =>
                        accountById.TryGetValue(e.AccountId, out var a)
                        && a.Type == AccountType.Expense
                        && e.Direction == EntryDirection.Debit);
                    if (expEntry != null)
                        return expEntry.Amount.Amount * getRate(expEntry.Amount.CurrencyCode, baseCurrency);
                    return tx.Entries
                        .Where(e => accountById.TryGetValue(e.AccountId, out var a)
                                    && a.Type == AccountType.Assets
                                    && e.Direction == EntryDirection.Credit)
                        .Sum(e => e.Amount.Amount * getRate(e.Amount.CurrencyCode, baseCurrency));
                });

                var income = txList.Sum(tx =>
                {
                    var incEntry = tx.Entries.FirstOrDefault(e =>
                        accountById.TryGetValue(e.AccountId, out var a)
                        && a.Type == AccountType.Income
                        && e.Direction == EntryDirection.Credit);
                    if (incEntry != null)
                        return incEntry.Amount.Amount * getRate(incEntry.Amount.CurrencyCode, baseCurrency);
                    return tx.Entries
                        .Where(e => accountById.TryGetValue(e.AccountId, out var a)
                                    && a.Type == AccountType.Assets
                                    && e.Direction == EntryDirection.Debit)
                        .Sum(e => e.Amount.Amount * getRate(e.Amount.CurrencyCode, baseCurrency));
                });

                return new MonthlyTotalRow
                {
                    Month = $"{mg.Key.Year:D4}-{mg.Key.Month:D2}",
                    Income = income,
                    Expense = expense
                };
            })
            .ToList();
    }

    public static IReadOnlyList<RecentTransactionItem> BuildRecentItems(
        IReadOnlyList<Transaction> transactions,
        IDictionary<Guid, Account> accountById,
        IDictionary<Guid, Category> categoryById,
        int count = 5)
    {
        var result = new List<RecentTransactionItem>();

        foreach (var tx in transactions.OrderByDescending(t => t.Date).Take(count))
        {
            var assetEntry = tx.Entries.FirstOrDefault(e =>
                accountById.TryGetValue(e.AccountId, out var a) && a.Type == AccountType.Assets);
            if (assetEntry == null) continue;

            accountById.TryGetValue(assetEntry.AccountId, out var account);
            var isIncome   = assetEntry.Direction == EntryDirection.Debit;
            var isTransfer = tx.Entries.Count >= 2 && tx.Entries.All(e =>
                accountById.TryGetValue(e.AccountId, out var a) && a.Type == AccountType.Assets);

            var categoryName = "—";
            var categoryEntry = tx.Entries.FirstOrDefault(e => e.CategoryId.HasValue);
            if (!isTransfer && categoryEntry?.CategoryId is Guid catId
                && categoryById.TryGetValue(catId, out var cat))
                categoryName = cat.Name;
            if (isTransfer)
                categoryName = "Перевод";

            result.Add(new RecentTransactionItem
            {
                Date = tx.Date.ToString("dd.MM.yyyy"),
                Amount = assetEntry.Amount.Amount,
                Currency = assetEntry.Amount.CurrencyCode,
                IsIncome = isIncome && !isTransfer,
                IsTransfer = isTransfer,
                CategoryName = categoryName,
                AccountName = account?.Name ?? "—",
                Description = tx.Description
            });
        }

        return result;
    }

    public static IReadOnlyList<CurrencyBalance> BuildCurrencyBalances(
        IReadOnlyList<Account> assetAccounts)
    {
        return assetAccounts
            .GroupBy(a => a.CurrencyCode)
            .Select(g => new CurrencyBalance
            {
                CurrencyCode = g.Key,
                Balance = g.Sum(a => a.Balance)
            })
            .OrderByDescending(c => c.Balance)
            .ToList();
    }
}