using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using System;
using System.Collections.Generic;
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

            // Group balances by currency
            var byCurrency = assets
                .GroupBy(a => a.CurrencyCode)
                .Select(g => new CurrencyBalance
                {
                    CurrencyCode = g.Key,
                    Balance = g.Sum(a => a.Balance)
                })
                .OrderByDescending(c => c.Balance)
                .ToList();

            foreach (var cb in byCurrency) CurrencyBalances.Add(cb);

            // Total in base currency
            TotalBalance = assets.Sum(a => a.Balance * _data.GetRate(a.CurrencyCode, _settings.BaseCurrency));
        }

        private void RefreshRecentTransactions()
        {
            RecentTransactions.Clear();

            var accountById = _data.Accounts.ToDictionary(a => a.Id);
            var categoryById = _data.Categories.ToDictionary(c => c.Id);

            // Take 5 most recent transactions
            var recent = _data.Transactions
                .OrderByDescending(t => t.Date)
                .Take(5)
                .ToList();

            foreach (var tx in recent)
            {
                // Find the "main" entry — the Assets account entry
                var assetEntry = tx.Entries.FirstOrDefault(e =>
                    accountById.TryGetValue(e.AccountId, out var a) && a.Type == AccountType.Assets);

                if (assetEntry == null) continue;

                accountById.TryGetValue(assetEntry.AccountId, out var account);
                var accountName = account?.Name ?? "—";
                var amount = assetEntry.Amount.Amount;
                var currency = assetEntry.Amount.CurrencyCode;
                var isIncome = assetEntry.Direction == EntryDirection.Debit;

                // Try to find category
                var categoryName = "—";
                var categoryEntry = tx.Entries.FirstOrDefault(e => e.CategoryId.HasValue);
                if (categoryEntry != null && categoryEntry.CategoryId.HasValue &&
                    categoryById.TryGetValue(categoryEntry.CategoryId.Value, out var cat))
                {
                    categoryName = cat.Name;
                }

                // Check if transfer
                var isTransfer = tx.Entries.Count >= 2 &&
                    tx.Entries.All(e => accountById.TryGetValue(e.AccountId, out var a) && a.Type == AccountType.Assets);

                if (isTransfer) categoryName = "Перевод";

                RecentTransactions.Add(new RecentTransactionItem
                {
                    Date = tx.Date.ToString("dd.MM.yyyy"),
                    Amount = amount,
                    Currency = currency,
                    IsIncome = isIncome && !isTransfer,
                    IsTransfer = isTransfer,
                    CategoryName = categoryName,
                    AccountName = accountName,
                    Description = tx.Description
                });
            }
        }

        private void RefreshMonthly()
        {
            var newMonthlySeries = new ObservableCollection<ISeries>();

            var now = DateTimeOffset.Now;

            // ── Smart date range: find months with actual data ──
            // 1. Get all distinct months that have transactions, sorted descending
            var allMonths = _data.Transactions
                .Select(t => new { t.Date.Year, t.Date.Month })
                .Distinct()
                .OrderByDescending(m => m.Year).ThenByDescending(m => m.Month)
                .ToList();

            if (allMonths.Count == 0)
            {
                // No data at all — clear chart
                MonthlySeries = newMonthlySeries;
                XAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
                YAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
                return;
            }

            // 2. Take up to 6 most recent months with data
            var selectedMonths = allMonths.Take(6).OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();

            // 3. Compute from/to based on selected months
            var first = selectedMonths.First();
            var last = selectedMonths.Last();
            var from = new DateTimeOffset(first.Year, first.Month, 1, 0, 0, 0, now.Offset);
            var to = new DateTimeOffset(last.Year, last.Month, 1, 0, 0, 0, now.Offset).AddMonths(1).AddTicks(-1);

            var monthlyRows = new ObservableCollection<MonthlyTotalRow>();
            MonthlyReport.RefreshMonthlyRows(_data, _settings, from, to, monthlyRows);

            var labels = monthlyRows.Select(r => r.Month).ToArray();

            var incomeGreen = SkiaSharp.SKColor.Parse("#00E676");
            newMonthlySeries.Add(new LineSeries<decimal>
            {
                Name = "Доходы",
                Values = monthlyRows.Select(r => r.Income).ToArray(),
                Stroke = new SolidColorPaint(incomeGreen, 2),
                Fill = new SolidColorPaint(incomeGreen.WithAlpha(60)),
                GeometrySize = 8,
                GeometryStroke = new SolidColorPaint(incomeGreen, 2),
                GeometryFill = new SolidColorPaint(incomeGreen),
                LineSmoothness = 0
            });

            var expenseRed = SkiaSharp.SKColor.Parse("#FF5252");
            newMonthlySeries.Add(new LineSeries<decimal>
            {
                Name = "Расходы",
                Values = monthlyRows.Select(r => r.Expense).ToArray(),
                Stroke = new SolidColorPaint(expenseRed, 2),
                Fill = new SolidColorPaint(expenseRed.WithAlpha(60)),
                GeometrySize = 8,
                GeometryStroke = new SolidColorPaint(expenseRed, 2),
                GeometryFill = new SolidColorPaint(expenseRed),
                LineSmoothness = 0
            });

            var gridColor = SkiaSharp.SKColor.Parse("#222845");
            var labelColor = SkiaSharp.SKColor.Parse("#9E9E9E");

            XAxes = new[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsPaint = new SolidColorPaint(labelColor),
                    SeparatorsPaint = null
                }
            };
            YAxes = new[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(labelColor),
                    SeparatorsPaint = new SolidColorPaint(gridColor, 1)
                }
            };

            MonthlySeries = newMonthlySeries;
        }
    }

    // ── Helper models for dashboard ────────────────────
    public class CurrencyBalance
    {
        public string CurrencyCode { get; set; } = "";
        public decimal Balance { get; set; }
    }

    public class RecentTransactionItem
    {
        public string Date { get; set; } = "";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "";
        public bool IsIncome { get; set; }
        public bool IsTransfer { get; set; }
        public string CategoryName { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string Description { get; set; } = "";

        public string FormattedAmount => IsTransfer
            ? $"{Amount:N2} {Currency}"
            : (IsIncome ? $"+{Amount:N2}" : $"−{Amount:N2}") + $" {Currency}";

        public string AmountColor => IsTransfer ? "#29B6F6" : (IsIncome ? "#00E676" : "#FF5252");

        // Arrow icon: ↗ income, ↙ expense, ↔ transfer
        public string DirectionIcon => IsTransfer ? "↔" : (IsIncome ? "↗" : "↙");
    }
}