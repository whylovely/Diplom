using Client.Models;
using Client.Services;
using Client.ViewModels.OperationWithReport;
using LiveChartsCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public partial class IncomeReport
    {
        public static decimal RefreshIncomeRows(
            IDataService data,
            SettingsService settings,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            ObservableCollection<CategoryShareRow> incomeRows)
        {
            incomeRows.Clear();
            if (!dateFrom.HasValue || !dateTo.HasValue) return 0;

            var txInRange   = TransactionAggregator.FilterByDateRange(data.Transactions, dateFrom, dateTo);
            var accountById = data.Accounts.ToDictionary(a => a.Id);
            var entries     = TransactionAggregator.GetIncomeEntries(txInRange, accountById);
            var rows        = TransactionAggregator.AggregateByCategoryRows(
                entries, data.Categories,
                (from, to) => data.GetRate(from, to),
                settings.BaseCurrency);

            foreach (var row in rows) incomeRows.Add(row);
            return incomeRows.Sum(r => r.Total);
        }

        public static void RefreshIncomeGroups(
            IDataService data,
            SettingsService settings,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            ObservableCollection<CategoryDetailGroup> incomeGroups)
        {
            incomeGroups.Clear();
            if (!dateFrom.HasValue || !dateTo.HasValue) return;

            var txInRange   = TransactionAggregator.FilterByDateRange(data.Transactions, dateFrom, dateTo);
            var accountById = data.Accounts.ToDictionary(a => a.Id);
            var entries     = TransactionAggregator.GetIncomeEntries(txInRange, accountById);
            var groups      = TransactionAggregator.AggregateByCategoryGroups(
                entries, txInRange, data.Categories,
                (from, to) => data.GetRate(from, to),
                settings.BaseCurrency);

            foreach (var group in groups) incomeGroups.Add(group);
        }

        public static void RefreshIncomeChart(
            IDataService data,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            ObservableCollection<CategoryShareRow> incomeShareRows,
            ObservableCollection<ISeries> incomePieSeries,
            decimal totalIncome,
            int topN,
            out decimal topIncomesSum,
            out decimal topIncomesShare,
            ObservableCollection<CategoryShareRow> incomeRows)
        {
            incomeShareRows.Clear();

            if (totalIncome <= 0)
            {
                topIncomesSum   = 0;
                topIncomesShare = 0;
                return;
            }

            var top = incomeRows
                .OrderByDescending(r => r.Total)
                .Take(topN)
                .Select(r => new CategoryShareRow
                {
                    CategoryName = r.CategoryName,
                    Total        = r.Total,
                    SharePercent = r.Total / totalIncome
                }).ToList();

            foreach (var row in top) incomeShareRows.Add(row);

            topIncomesSum   = top.Sum(r => r.Total);
            topIncomesShare = Math.Round((topIncomesSum / totalIncome) * 100m, 2);

            incomePieSeries.Clear();
            ExpenseReport.BuildPieSeries(incomeShareRows, incomePieSeries);
        }
    }
}