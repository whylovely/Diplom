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
        public static decimal RefreshExpenseRows(
            IDataService _data, 
            DateTimeOffset DateFrom, 
            DateTimeOffset DateTo, 
            ObservableCollection<CategoryShareRow> ExpenseRows) // список суммы на категории
        {
            ExpenseRows.Clear();
            var txInRange = _data.Transactions.Where(t => t.Date >= DateFrom && t.Date <= DateTo).ToList();
            var accountById = _data.Accounts.ToDictionary(a => a.Id);

            var expenseGroups = txInRange
                .SelectMany(t => t.Entries)
                .Where(e =>
                {
                    var acc = accountById[e.AccountId];
                    return acc.Type == AccountType.Expense && e.Direction == EntryDirection.Debit;
                })
                .GroupBy(e => e.CategoryId)
                .Select(g =>
                {
                    var catName = _data.Categories.FirstOrDefault(c => c.Id == g.Key)?.Name ?? "—";
                    return new CategoryShareRow
                    {
                        CategoryName = catName,
                        Total = g.Sum(x => x.Amount.Amount)
                    };
                })
                .OrderByDescending(r => r.Total);

            foreach (var row in expenseGroups) ExpenseRows.Add(row);

            return ExpenseRows.Sum(r => r.Total);
        }

        public static void RefreshExpenseGroups(
            IDataService _data, 
            DateTimeOffset DateFrom, 
            DateTimeOffset DateTo, 
                ObservableCollection<CategoryDetailGroup> ExpenseGroups)    // за какой день
            {
            ExpenseGroups.Clear();
            var txInRange = _data.Transactions.Where(t => t.Date >= DateFrom && t.Date <= DateTo).ToList();
            var accountById = _data.Accounts.ToDictionary(a => a.Id);

            var groups = txInRange
                .SelectMany(t => t.Entries.Select(e => new { Entry = e, Tx = t }))
                .Where(x =>
                {
                    var acc = accountById[x.Entry.AccountId];
                    return acc.Type == AccountType.Expense && x.Entry.Direction == EntryDirection.Debit;
                })
                .GroupBy(x => x.Entry.CategoryId)
                .Select(g =>
                {
                    var catName = _data.Categories.FirstOrDefault(c => c.Id == g.Key)?.Name ?? "—";
                    var days = g
                        .GroupBy(x => x.Tx.Date.Date)
                        .OrderBy(d => d.Key)
                        .Select(d => new DailyDetailRow
                        {
                            Date = d.Key.ToString("dd.MM.yyyy"),
                            Amount = d.Sum(x => x.Entry.Amount.Amount),
                            Description = string.Join(", ", d.Select(x => x.Tx.Description).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
                        })
                        .ToList();

                    return new CategoryDetailGroup
                    {
                        CategoryName = catName,
                        Total = g.Sum(x => x.Entry.Amount.Amount),
                        Days = days
                    };
                })
                .OrderByDescending(r => r.Total);

            foreach (var group in groups) ExpenseGroups.Add(group);
        }

        public static void RefreshExpenseChart(
            IDataService _data, 
            DateTimeOffset DateFrom, 
            DateTimeOffset DateTo, 
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
            foreach (var r in ExpenseShareRows)
                ExpensePieSeries.Add(new PieSeries<decimal> { Values = new[] { r.Total }, Name = r.CategoryName });
        }

        
    }
}