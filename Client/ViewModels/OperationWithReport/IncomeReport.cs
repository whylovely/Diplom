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
    public partial class IncomeReport   // класс-помощник для доходов
    {
        /// <summary>
        /// Определяет доходные записи. Приоритет: Income-entry (двойная запись).
        /// Фолбэк: Assets+Debit (серверные транзакции без технических счетов).
        /// </summary>
        private static IEnumerable<Entry> GetIncomeEntries(
            IList<Transaction> txInRange,
            IDictionary<Guid, Account> accountById)
        {
            foreach (var tx in txInRange)
            {
                var incomeEntry = tx.Entries.FirstOrDefault(e =>
                    accountById.TryGetValue(e.AccountId, out var acc)
                    && acc.Type == AccountType.Income && e.Direction == EntryDirection.Credit);

                if (incomeEntry != null)
                {
                    yield return incomeEntry;
                    continue;
                }

                foreach (var e in tx.Entries)
                {
                    if (accountById.TryGetValue(e.AccountId, out var acc)
                        && acc.Type == AccountType.Assets && e.Direction == EntryDirection.Debit)
                    {
                        yield return e;
                    }
                }
            }
        }

        public static decimal RefreshIncomeRows(
            IDataService dataServ,
            SettingsService settings,
            DateTimeOffset? DateFrom, 
            DateTimeOffset? DateTo, 
            ObservableCollection<CategoryShareRow> IncomeRows)  // список суммы за доходы
        {
            IncomeRows.Clear();
            if (!DateFrom.HasValue || !DateTo.HasValue) return 0;
            var txInRange = dataServ.Transactions.Where(t => t.Date.Date >= DateFrom.Value.Date && t.Date.Date <= DateTo.Value.Date).ToList(); 
            var accountById = dataServ.Accounts.ToDictionary(a => a.Id);

            var incomeGroups = GetIncomeEntries(txInRange, accountById)
                .GroupBy(e => e.CategoryId)
                .Select(g =>
                {
                    var catName = dataServ.Categories.FirstOrDefault(c => c.Id == g.Key)?.Name ?? "—";
                    return new CategoryShareRow
                    {
                        CategoryName = catName,
                        Total = g.Sum(x => x.Amount.Amount * dataServ.GetRate(x.Amount.CurrencyCode, settings.BaseCurrency))
                    };
                })
                .OrderByDescending(r => r.Total);

            foreach (var row in incomeGroups) IncomeRows.Add(row);

            return IncomeRows.Sum(r => r.Total);
        }

        public static void RefreshIncomeGroups(
            IDataService dataServ,
            SettingsService settings,
            DateTimeOffset? DateFrom, 
            DateTimeOffset? DateTo, 
            ObservableCollection<CategoryDetailGroup> IncomeGroups) // доход за день
        {
            IncomeGroups.Clear();
            if (!DateFrom.HasValue || !DateTo.HasValue) return;
            var txInRange = dataServ.Transactions.Where(t => t.Date.Date >= DateFrom.Value.Date && t.Date.Date <= DateTo.Value.Date).ToList();
            var accountById = dataServ.Accounts.ToDictionary(a => a.Id);

            var entries = GetIncomeEntries(txInRange, accountById).ToList();
            var txById = txInRange.SelectMany(t => t.Entries.Select(e => new { e.Id, Tx = t }))
                .ToDictionary(x => x.Id, x => x.Tx);

            var groups = entries
                .Select(e => new { Entry = e, Tx = txById.GetValueOrDefault(e.Id) })
                .Where(x => x.Tx != null)
                .GroupBy(x => x.Entry.CategoryId)
                .Select(g =>
                {
                    var catName = dataServ.Categories.FirstOrDefault(c => c.Id == g.Key)?.Name ?? "—";
                    var days = g
                        .GroupBy(x => x.Tx.Date.Date)
                        .OrderBy(d => d.Key)
                        .Select(d => new DailyDetailRow
                        {
                            Date = d.Key.ToString("dd.MM.yyyy"),
                            Amount = d.Sum(x => x.Entry.Amount.Amount * dataServ.GetRate(x.Entry.Amount.CurrencyCode, settings.BaseCurrency)),
                            Description = string.Join(", ", d.Select(x => x.Tx.Description).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
                        })
                        .ToList();

                    return new CategoryDetailGroup
                    {
                        CategoryName = catName,
                        Total = g.Sum(x => x.Entry.Amount.Amount * dataServ.GetRate(x.Entry.Amount.CurrencyCode, settings.BaseCurrency)),
                        Days = days
                    };
                })
                .OrderByDescending(r => r.Total);

            foreach (var group in groups) IncomeGroups.Add(group);
        }

        public static void RefreshIncomeChart(
            IDataService dataServ,
            DateTimeOffset? DateFrom,
            DateTimeOffset? DateTo,
            ObservableCollection<CategoryShareRow> IncomeShareRows,
            ObservableCollection<ISeries> IncomePieSeries,
            decimal TotalIncome,
            int TopN,
            decimal TopIncomesSum,
            decimal TopIncomesShare,    
            ObservableCollection<CategoryShareRow> IncomeRows)  // диаграмма доходов
        {
            IncomeShareRows.Clear();

            if (TotalIncome <= 0)
            {
                TopIncomesSum = 0;
                TopIncomesShare = 0;
                return;
            }

            var top = IncomeRows
                .OrderByDescending(r => r.Total)
                .Take(TopN)
                .Select(r => new CategoryShareRow
                {
                    CategoryName = r.CategoryName,
                    Total = r.Total,
                    SharePercent = r.Total / TotalIncome
                }).ToList();

            foreach (var row in top) IncomeShareRows.Add(row);

            TopIncomesSum = top.Sum(r => r.Total);
            TopIncomesShare = Math.Round((TopIncomesSum / TotalIncome) * 100m, 2);

            IncomePieSeries.Clear();
            var colors = new[] { "#B9F6CA", "#FF9E80", "#80D8FF", "#EA80FC", "#FFD180", "#FFAB91", "#CE93D8", "#80CBC4" };
            int i = 0;
            foreach (var r in IncomeShareRows)
            {
                var hex = colors[i % colors.Length];
                var skColor = SkiaSharp.SKColor.Parse(hex);
                IncomePieSeries.Add(new PieSeries<decimal>
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