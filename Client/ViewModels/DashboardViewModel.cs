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

        // ── Expense Donut ──────────────────────────────────
        public ObservableCollection<ISeries> ExpensePieSeries { get; } = new();
        public ObservableCollection<CategoryLegendItem> ExpenseLegend { get; } = new();

        // ── Monthly Dynamics ───────────────────────────────
        public ObservableCollection<ISeries> MonthlySeries { get; } = new();
        [ObservableProperty] private LiveChartsCore.SkiaSharpView.Axis[] _xAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis[]>() is null ? new Axis[0] : new Axis[0];
        [ObservableProperty] private LiveChartsCore.SkiaSharpView.Axis[] _yAxes = Array.Empty<Axis>();

        [RelayCommand]
        public void Refresh()
        {
            try
            {
                RefreshAccounts();
                RefreshTotalBalance();
                RefreshExpenseDonut();
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

        private void RefreshExpenseDonut()
        {
            ExpensePieSeries.Clear();
            ExpenseLegend.Clear();

            var now = DateTimeOffset.Now;
            var from = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
            var to = now;

            var accountById = _data.Accounts.ToDictionary(a => a.Id);
            var txInRange = _data.Transactions.Where(t => t.Date >= from && t.Date <= to).ToList();

            var expenseGroups = txInRange
                .SelectMany(t => t.Entries)
                .Where(e =>
                {
                    if (!accountById.TryGetValue(e.AccountId, out var acc)) return false;
                    return acc.Type == AccountType.Expense && e.Direction == EntryDirection.Debit;
                })
                .GroupBy(e => e.CategoryId)
                .Select(g =>
                {
                    var catName = _data.Categories.FirstOrDefault(c => c.Id == g.Key)?.Name ?? "—";
                    return new { Name = catName, Total = g.Sum(x => x.Amount.Amount) };
                })
                .OrderByDescending(r => r.Total)
                .Take(5)
                .ToList();

            var totalExp = expenseGroups.Sum(r => r.Total);
            var colors = new[] { "#B9F6CA", "#FF9E80", "#80D8FF", "#EA80FC", "#FFD180", "#FFAB91", "#CE93D8", "#80CBC4" };
            int i = 0;

            foreach (var r in expenseGroups)
            {
                var hex = colors[i % colors.Length];
                var skColor = SkiaSharp.SKColor.Parse(hex);
                ExpensePieSeries.Add(new PieSeries<decimal>
                {
                    Values = new[] { r.Total },
                    Name = r.Name,
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 20,
                    HoverPushout = 0,
                    Pushout = 2,
                    Stroke = null,
                    Fill = new SolidColorPaint(skColor)
                });

                var sharePercent = totalExp > 0 ? Math.Round((double)(r.Total / totalExp) * 100, 1) : 0;
                ExpenseLegend.Add(new CategoryLegendItem
                {
                    Name = r.Name,
                    Color = hex,
                    SharePercent = sharePercent
                });
                i++;
            }
        }

        private void RefreshMonthly()
        {
            MonthlySeries.Clear();

            var now = DateTimeOffset.Now;
            var from = now.AddMonths(-7);
            var monthlyRows = new ObservableCollection<MonthlyTotalRow>();
            MonthlyReport.RefreshMonthlyRows(_data, _settings, from, now, monthlyRows);

            var labels = monthlyRows.Select(r => r.Month).ToArray();

            var incomeGreen = SkiaSharp.SKColor.Parse("#00E676");
            MonthlySeries.Add(new LineSeries<decimal>
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
            MonthlySeries.Add(new LineSeries<decimal>
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
        }
    }

    // ── Helper models for dashboard ────────────────────
    public class CurrencyBalance
    {
        public string CurrencyCode { get; set; } = "";
        public decimal Balance { get; set; }
    }

    public class CategoryLegendItem
    {
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#FFFFFF";
        public double SharePercent { get; set; }
    }
}