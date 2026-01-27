using Client.Services;
using Client.Models;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace Client.ViewModels
{
    public sealed partial class ReportViewModel : ViewModelBase
    {
        private readonly IDataService _data;

        public ObservableCollection<CategoryTotalRow> ExpenseRows { get; } = new();
        public ObservableCollection<CategoryTotalRow> IncomeRows { get; } = new();
        public ObservableCollection<MonthlyTotalRow> MothlyRows { get; } = new();
        public ObservableCollection<CategoryShareRow> ExpenseShareRows { get; } = new();

        public ObservableCollection<ISeries> ExpensePieSeries { get; } = new(); // Свойство графика

        [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddMonths(-1);
        [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
        [ObservableProperty] private decimal _totalExpense;
        [ObservableProperty] private decimal _totalIncome;
        [ObservableProperty] private decimal _net;

        [ObservableProperty] private int _topN = 5; // количество показываемых категорий
        [ObservableProperty] private decimal _topExpensesSum;
        [ObservableProperty] private decimal _topExpensesShare;

        public ReportViewModel(IDataService data)
        {
            _data = data;
            Refresh();
        }

        partial void OnTopNChanged(int value)
        {
            if (value < 1)
                TopN = 1;
            Refresh();
        }

        [RelayCommand]
        public void Refresh()
        {
            ExpenseRows.Clear();
            IncomeRows.Clear();

            var txInRange = _data.Transactions
                .Where(t => t.Date >= DateFrom && t.Date <= DateTo)
                .ToList();  // Период

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
                    var catName = _data.Categories.FirstOrDefault(c => c.Id == g.Key).Name;
                    return new CategoryTotalRow
                    {
                        CategoryName = catName,
                        Total = g.Sum(x => x.Amount.Amount)
                    };
                })
                .OrderByDescending(r => r.Total);   // Подсчет расходов

            foreach (var row in expenseGroups)
                ExpenseRows.Add(row);

            var incomeGroups = txInRange
                .SelectMany(t => t.Entries)
                .Where(e =>
                {
                    var acc = accountById[e.AccountId];
                    return acc.Type == AccountType.Income && e.Direction == EntryDirection.Credit;
                })
                .GroupBy(e => e.CategoryId)
                .Select(g =>
                {
                    var catName = _data.Categories.FirstOrDefault(c => c.Id == g.Key).Name;
                    return new CategoryTotalRow
                    {
                        CategoryName = catName,
                        Total = g.Sum(x => x.Amount.Amount)
                    };
                })
                .OrderByDescending(r => r.Total);   // Подсчет доходов

            foreach (var row in incomeGroups)
                IncomeRows.Add(row);

            _totalExpense = ExpenseRows.Sum(r => r.Total); 
            _totalIncome = IncomeRows.Sum(r => r.Total);
            _net = _totalIncome - _totalExpense;

            // Расчет помесячных итогов
            MothlyRows.Clear();

            var mothlyGroups = txInRange
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

            foreach (var mg in mothlyGroups)
            {
                var enteries = mg.SelectMany(t => t.Entries);

                var expense = enteries
                    .Where(e =>
                    {
                        var acc = accountById[e.AccountId];
                        return acc.Type == AccountType.Expense && e.Direction == EntryDirection.Debit;
                    })
                    .Sum(e => e.Amount.Amount);

                var income = enteries
                    .Where(e =>
                    {
                        var acc = accountById[e.AccountId];
                        return acc.Type == AccountType.Income && e.Direction == EntryDirection.Credit;
                    })
                    .Sum(e => e.Amount.Amount);

                MothlyRows.Add(new MonthlyTotalRow
                {
                    Month = $"{mg.Key.Year:D4}-{mg.Key.Month:D2}",
                    Income = income,
                    Expense = expense
                });
            }

            // Расчет долей расходов по категориям
            ExpenseShareRows.Clear();

            if (TotalExpense <=0)
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

            foreach (var row in top)
                ExpenseShareRows.Add(row);

            TopExpensesSum = top.Sum(r => r.Total);
            TopExpensesShare = Math.Round((TopExpensesSum / TotalExpense) * 100m, 2);

            // Построение графика
            ExpensePieSeries.Clear();

            foreach (var r in ExpenseShareRows)
            {
                ExpensePieSeries.Add(new PieSeries<decimal>
                {
                    Values = new [] { r.Total },
                    Name = r.CategoryName
                });
            }
        }
    }
}