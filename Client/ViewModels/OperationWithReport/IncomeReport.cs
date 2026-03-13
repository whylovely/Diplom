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
        public static decimal RefreshIncomeRows(
            IDataService dataServ,
            SettingsService settings,
            DateTimeOffset DateFrom, 
            DateTimeOffset DateTo, 
            ObservableCollection<CategoryShareRow> IncomeRows)  // список суммы за доходы
        {
            IncomeRows.Clear();
            var txInRange = dataServ.Transactions.Where(t => t.Date >= DateFrom && t.Date <= DateTo).ToList(); 
            var accountById = dataServ.Accounts.ToDictionary(a => a.Id);

            var incomeGroups = txInRange
                .SelectMany(t => t.Entries)
                .Where(e =>
                {
                    if (!accountById.TryGetValue(e.AccountId, out var acc)) return false;
                    return acc.Type == AccountType.Income && e.Direction == EntryDirection.Credit;
                })
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
            DateTimeOffset DateFrom, 
            DateTimeOffset DateTo, 
            ObservableCollection<CategoryDetailGroup> IncomeGroups) // доход за день
        {
            IncomeGroups.Clear();
            var txInRange = dataServ.Transactions.Where(t => t.Date >= DateFrom && t.Date <= DateTo).ToList();
            var accountById = dataServ.Accounts.ToDictionary(a => a.Id);

            var groups = txInRange
                .SelectMany(t => t.Entries.Select(e => new { Entry = e, Tx = t }))
                .Where(x =>
                {
                    if (!accountById.TryGetValue(x.Entry.AccountId, out var acc)) return false;
                    return acc.Type == AccountType.Income && x.Entry.Direction == EntryDirection.Credit;
                })
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
            DateTimeOffset DateFrom,
            DateTimeOffset DateTo,
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
            foreach (var r in IncomeShareRows)
                IncomePieSeries.Add(new PieSeries<decimal> { Values = new[] { r.Total }, Name = r.CategoryName });
        }
    }
}