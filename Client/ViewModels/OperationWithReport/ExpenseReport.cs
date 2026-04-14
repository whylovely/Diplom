using Client.Models;
using Client.Services;
using Client.ViewModels.OperationWithReport;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public partial class ExpenseReport  // класс-помощник для отчета с расходами
    {
        private static IEnumerable<Entry> GetExpenseEntries(
            IList<Transaction> txInRange,
            IDictionary<Guid, Account> accountById)
        {
            foreach (var tx in txInRange)
            {
                var expenseEntry = tx.Entries.FirstOrDefault(e =>
                    accountById.TryGetValue(e.AccountId, out var acc)
                    && acc.Type == AccountType.Expense && e.Direction == EntryDirection.Debit);

                if (expenseEntry != null)
                {
                    yield return expenseEntry;
                    continue;
                }

                foreach (var e in tx.Entries)
                {
                    if (accountById.TryGetValue(e.AccountId, out var acc)
                        && acc.Type == AccountType.Assets && e.Direction == EntryDirection.Credit)
                    {
                        yield return e;
                    }
                }
            }
        }

        public static decimal RefreshExpenseRows(
            IDataService _data, 
            SettingsService _settings,
            DateTimeOffset? DateFrom, 
            DateTimeOffset? DateTo, 
            ObservableCollection<CategoryShareRow> ExpenseRows) // список суммы на категории
        {
            ExpenseRows.Clear();
            if (!DateFrom.HasValue || !DateTo.HasValue) return 0;
            var txInRange = _data.Transactions.Where(t => t.Date.Date >= DateFrom.Value.Date && t.Date.Date <= DateTo.Value.Date).ToList();
            var accountById = _data.Accounts.ToDictionary(a => a.Id);

            var expenseGroups = GetExpenseEntries(txInRange, accountById)
                .GroupBy(e => e.CategoryId)
                .Select(g =>
                {
                    var catName = _data.Categories.FirstOrDefault(c => c.Id == g.Key)?.Name ?? "—";
                    return new CategoryShareRow
                    {
                        CategoryName = catName,
                        Total = g.Sum(x => x.Amount.Amount * _data.GetRate(x.Amount.CurrencyCode, _settings.BaseCurrency))
                    };
                })
                .OrderByDescending(r => r.Total);

            foreach (var row in expenseGroups) ExpenseRows.Add(row);

            return ExpenseRows.Sum(r => r.Total);
        }

        public static void RefreshExpenseGroups(
            IDataService _data, 
            SettingsService _settings,
            DateTimeOffset? DateFrom, 
            DateTimeOffset? DateTo, 
                ObservableCollection<CategoryDetailGroup> ExpenseGroups)    // за какой день
            {
            ExpenseGroups.Clear();
            if (!DateFrom.HasValue || !DateTo.HasValue) return;
            var txInRange = _data.Transactions.Where(t => t.Date.Date >= DateFrom.Value.Date && t.Date.Date <= DateTo.Value.Date).ToList();
            var accountById = _data.Accounts.ToDictionary(a => a.Id);

            var entries = GetExpenseEntries(txInRange, accountById).ToList();
            var txById = txInRange.SelectMany(t => t.Entries.Select(e => new { e.Id, Tx = t }))
                .ToDictionary(x => x.Id, x => x.Tx);

            var groups = entries
                .Select(e => new { Entry = e, Tx = txById.GetValueOrDefault(e.Id) })
                .Where(x => x.Tx != null)
                .GroupBy(x => x.Entry.CategoryId)
                .Select(g =>
                {
                    var catName = _data.Categories.FirstOrDefault(c => c.Id == g.Key)?.Name ?? "—";
                    var days = g
                        .GroupBy(x => x.Tx!.Date.Date)
                        .OrderBy(d => d.Key)
                        .Select(d => new DailyDetailRow
                        {
                            Date = d.Key.ToString("dd.MM.yyyy"),
                            Amount = d.Sum(x => x.Entry.Amount.Amount * _data.GetRate(x.Entry.Amount.CurrencyCode, _settings.BaseCurrency)),
                            Description = string.Join(", ", d.Select(x => x.Tx!.Description).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
                        })
                        .ToList();

                    return new CategoryDetailGroup
                    {
                        CategoryName = catName,
                        Total = g.Sum(x => x.Entry.Amount.Amount * _data.GetRate(x.Entry.Amount.CurrencyCode, _settings.BaseCurrency)),
                        Days = days
                    };
                })
                .OrderByDescending(r => r.Total);

            foreach (var group in groups) ExpenseGroups.Add(group);
        }

        public static void RefreshExpenseChart(
            IDataService _data, 
            DateTimeOffset? DateFrom, 
            DateTimeOffset? DateTo, 
            ObservableCollection<CategoryShareRow> ExpenseShareRows, 
            ObservableCollection<ISeries> ExpensePieSeries, 
            decimal TotalExpense,
            int TopN,
            decimal TopExpensesSum,
            decimal TopExpensesShare,
            ObservableCollection<CategoryShareRow> ExpenseRows) // диаграмма расходов
        {
            ExpenseShareRows.Clear();

            if (TotalExpense <= 0)
            {
                TopExpensesSum = 0;
                TopExpensesShare = 0;
                return;
            }

            var top = ExpenseRows
                .OrderByDescending(r => r.Total)
                .Take(TopN)
                .Select(r => new CategoryShareRow
                {
                    CategoryName = r.CategoryName,
                    Total = r.Total,
                    SharePercent = r.Total / TotalExpense
                }).ToList();

            foreach (var row in top) ExpenseShareRows.Add(row);

            TopExpensesSum = top.Sum(r => r.Total);
            TopExpensesShare = Math.Round((TopExpensesSum / TotalExpense) * 100m, 2);

            ExpensePieSeries.Clear();
            var colors = new[] { "#B9F6CA", "#FF9E80", "#80D8FF", "#EA80FC", "#FFD180", "#FFAB91", "#CE93D8", "#80CBC4" };
            int i = 0;
            foreach (var r in ExpenseShareRows)
            {
                var hex = colors[i % colors.Length];
                var skColor = SkiaSharp.SKColor.Parse(hex);
                ExpensePieSeries.Add(new PieSeries<decimal>
                {
                    Values = new[] { r.Total },
                    Name = r.CategoryName,
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 20,
                    HoverPushout = 0,
                    Pushout = 2,
                    Stroke = null,
                    Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(skColor)
                });
                i++;
            }
        }
    }
}