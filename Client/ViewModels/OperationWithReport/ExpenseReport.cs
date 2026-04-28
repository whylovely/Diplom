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
    public partial class ExpenseReport
    {
        public static decimal RefreshExpenseRows(
            IDataService data,
            SettingsService settings,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            ObservableCollection<CategoryShareRow> expenseRows)
        {
            expenseRows.Clear();
            if (!dateFrom.HasValue || !dateTo.HasValue) return 0;

            var txInRange  = TransactionAggregator.FilterByDateRange(data.Transactions, dateFrom, dateTo);
            var accountById = data.Accounts.ToDictionary(a => a.Id);
            var entries    = TransactionAggregator.GetExpenseEntries(txInRange, accountById);
            var rows       = TransactionAggregator.AggregateByCategoryRows(
                entries, data.Categories,
                (from, to) => data.GetRate(from, to),
                settings.BaseCurrency);

            foreach (var row in rows) expenseRows.Add(row);
            return expenseRows.Sum(r => r.Total);
        }

        public static void RefreshExpenseGroups(
            IDataService data,
            SettingsService settings,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            ObservableCollection<CategoryDetailGroup> expenseGroups)
        {
            expenseGroups.Clear();
            if (!dateFrom.HasValue || !dateTo.HasValue) return;

            var txInRange   = TransactionAggregator.FilterByDateRange(data.Transactions, dateFrom, dateTo);
            var accountById = data.Accounts.ToDictionary(a => a.Id);
            var entries     = TransactionAggregator.GetExpenseEntries(txInRange, accountById);
            var groups      = TransactionAggregator.AggregateByCategoryGroups(
                entries, txInRange, data.Categories,
                (from, to) => data.GetRate(from, to),
                settings.BaseCurrency);

            foreach (var group in groups) expenseGroups.Add(group);
        }

        public static void RefreshExpenseChart(
            IDataService data,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            ObservableCollection<CategoryShareRow> expenseShareRows,
            ObservableCollection<ISeries> expensePieSeries,
            decimal totalExpense,
            int topN,
            out decimal topExpensesSum,
            out decimal topExpensesShare,
            ObservableCollection<CategoryShareRow> expenseRows)
        {
            expenseShareRows.Clear();

            if (totalExpense <= 0)
            {
                topExpensesSum   = 0;
                topExpensesShare = 0;
                return;
            }

            var top = expenseRows
                .OrderByDescending(r => r.Total)
                .Take(topN)
                .Select(r => new CategoryShareRow
                {
                    CategoryName = r.CategoryName,
                    Total        = r.Total,
                    SharePercent = r.Total / totalExpense
                }).ToList();

            foreach (var row in top) expenseShareRows.Add(row);

            topExpensesSum   = top.Sum(r => r.Total);
            topExpensesShare = Math.Round((topExpensesSum / totalExpense) * 100m, 2);

            expensePieSeries.Clear();
            BuildPieSeries(expenseShareRows, expensePieSeries);
        }

        internal static void BuildPieSeries(
            ObservableCollection<CategoryShareRow> shareRows,
            ObservableCollection<ISeries> pieSeries)
        {
            var colors = new[] { "#B9F6CA", "#FF9E80", "#80D8FF", "#EA80FC", "#FFD180", "#FFAB91", "#CE93D8", "#80CBC4" };
            int i = 0;
            foreach (var r in shareRows)
            {
                var skColor = SkiaSharp.SKColor.Parse(colors[i % colors.Length]);
                pieSeries.Add(new PieSeries<decimal>
                {
                    Values              = new[] { r.Total },
                    Name                = r.CategoryName,
                    InnerRadius         = 50,
                    MaxRadialColumnWidth = 20,
                    HoverPushout        = 0,
                    Pushout             = 2,
                    Stroke              = null,
                    Fill                = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(skColor)
                });
                i++;
            }
        }
    }
}