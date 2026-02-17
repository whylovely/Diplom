using Client.Models;
using Client.Services;
using Client.ViewModels.OperationWithReport;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public partial class MonthlyReport
    {
        public static void RefreshMonthlyRows(
            IDataService _data,
            DateTimeOffset DateFrom,
            DateTimeOffset DateTo,
            ObservableCollection<MonthlyTotalRow> MonthlyRows)
        {
            MonthlyRows.Clear();
            var txInRange = _data.Transactions.Where(t => t.Date >= DateFrom && t.Date <= DateTo).ToList();
            var accountById = _data.Accounts.ToDictionary(a => a.Id);

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

                MonthlyRows.Add(new MonthlyTotalRow
                {
                    Month = $"{mg.Key.Year:D4}-{mg.Key.Month:D2}",
                    Income = income,
                    Expense = expense
                });
            }
        }

        public static void RefreshMonthlyChart(
            ObservableCollection<MonthlyTotalRow> MonthlyRows,
            ObservableCollection<ISeries> MonthlySeries,
            ObservableCollection<string> MonthlyLabels,
            out Axis[] XAxes,
            out Axis[] YAxes)
        {
            MonthlySeries.Clear();
            MonthlyLabels.Clear();

            foreach (var r in MonthlyRows) MonthlyLabels.Add(r.Month);

            var incomeValues = MonthlyRows.Select(r => r.Income).ToArray();
            var expenseValues = MonthlyRows.Select(r => r.Expense).ToArray();

            MonthlySeries.Add(new ColumnSeries<decimal> { Name = "Доходы", Values = incomeValues });
            MonthlySeries.Add(new ColumnSeries<decimal> { Name = "Расходы", Values = expenseValues });

            XAxes = new[] { new Axis { Labels = MonthlyLabels.ToArray() } };
            YAxes = new[] { new Axis() };
        }
    }
}
