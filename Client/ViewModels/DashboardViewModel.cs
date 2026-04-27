using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public sealed partial class DashboardViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly SettingsService _settings;

        public DashboardViewModel(IDataService data, SettingsService settings)
        {
            _data = data;
            _settings = settings;

            _data.DataChanged += () => Refresh();
            _settings.SettingsChanged += () => Refresh();
            Refresh();
        }

        // ── Account Cards ──────────────────────────────────
        public ObservableCollection<Account> AssetAccounts { get; } = new();

        // ── Total Balance ──────────────────────────────────
        [ObservableProperty] private decimal _totalBalance;
        public string BaseCurrency => _settings.BaseCurrency;
        public ObservableCollection<CurrencyBalance> CurrencyBalances { get; } = new();

        // ── Recent Transactions ────────────────────────────
        public ObservableCollection<RecentTransactionItem> RecentTransactions { get; } = new();

        // ── Monthly Dynamics ───────────────────────────────
        [ObservableProperty] private ObservableCollection<ISeries> _monthlySeries = new();
        [ObservableProperty] private LiveChartsCore.SkiaSharpView.Axis[] _xAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis[]>() is null ? new Axis[0] : new Axis[0];
        [ObservableProperty] private LiveChartsCore.SkiaSharpView.Axis[] _yAxes = Array.Empty<Axis>();

        [RelayCommand]
        public void Refresh()
        {
            try
            {
                RefreshAccounts();
                RefreshTotalBalance();
                RefreshRecentTransactions();
                RefreshMonthly();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardVM] {ex.Message}");
            }
        }

        private void RefreshAccounts()
        {
            AssetAccounts.Clear();
            foreach (var a in _data.Accounts.Where(a => a.Type == AccountType.Assets && !a.IsDeleted))
                AssetAccounts.Add(a);
        }

        private void RefreshTotalBalance()
        {
            CurrencyBalances.Clear();

            var assets = _data.Accounts.Where(a => a.Type == AccountType.Assets && !a.IsDeleted).ToList();

            foreach (var cb in TransactionAggregator.BuildCurrencyBalances(assets))
                CurrencyBalances.Add(cb);

            TotalBalance = assets.Sum(a => a.Balance * _data.GetRate(a.CurrencyCode, _settings.BaseCurrency));
        }

        private void RefreshRecentTransactions()
        {
            RecentTransactions.Clear();

            var accountById  = _data.Accounts.ToDictionary(a => a.Id);
            var categoryById = _data.Categories.ToDictionary(c => c.Id);

            foreach (var item in TransactionAggregator.BuildRecentItems(_data.Transactions, accountById, categoryById))
                RecentTransactions.Add(item);
        }

        private void RefreshMonthly()
        {
            var now = DateTimeOffset.Now;

            // Определяем диапазон: до 6 последних месяцев с данными
            var allMonths = _data.Transactions
                .Select(t => new { t.Date.Year, t.Date.Month })
                .Distinct()
                .OrderByDescending(m => m.Year).ThenByDescending(m => m.Month)
                .ToList();

            if (allMonths.Count == 0)
            {
                MonthlySeries = new ObservableCollection<ISeries>();
                XAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
                YAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
                return;
            }

            var selected = allMonths.Take(6).OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();
            var from = new DateTimeOffset(selected.First().Year, selected.First().Month, 1, 0, 0, 0, now.Offset);
            var to   = new DateTimeOffset(selected.Last().Year,  selected.Last().Month,  1, 0, 0, 0, now.Offset)
                           .AddMonths(1).AddTicks(-1);

            var monthlyRows = new ObservableCollection<MonthlyTotalRow>();
            MonthlyReport.RefreshMonthlyRows(_data, _settings, from, to, monthlyRows);

            var newSeries = new ObservableCollection<ISeries>();
            var labels    = new ObservableCollection<string>();
            MonthlyReport.RefreshMonthlyChart(monthlyRows, newSeries, labels,
                out var xAxes, out var yAxes);

            MonthlySeries = newSeries;
            XAxes         = xAxes;
            YAxes         = yAxes;
        }
    }

}