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
    public partial class MonthlyReport  // класс-помощник для отчетов по месяцам
    {
        public static void RefreshMonthlyRows(
            IDataService _data, 
            SettingsService _settings,
            DateTimeOffset? DateFrom, 
            DateTimeOffset? DateTo,
            ObservableCollection<MonthlyTotalRow> MonthlyRows)  // сортировка категорий по месяцам
        {
            MonthlyRows.Clear();
            if (!DateFrom.HasValue || !DateTo.HasValue) return;
            var txInRange = _data.Transactions.Where(t => t.Date.Date >= DateFrom.Value.Date && t.Date.Date <= DateTo.Value.Date).ToList();
            var accountById = _data.Accounts.ToDictionary(a => a.Id);

            var mothlyGroups = txInRange
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

            foreach (var mg in mothlyGroups)
            {
                var txList = mg.ToList();
                decimal expense = 0;
                foreach (var tx in txList)
                {
                    var expEntry = tx.Entries.FirstOrDefault(e =>
                        accountById.TryGetValue(e.AccountId, out var a) && a.Type == AccountType.Expense && e.Direction == EntryDirection.Debit);
                    if (expEntry != null)
                    {
                        expense += expEntry.Amount.Amount * _data.GetRate(expEntry.Amount.CurrencyCode, _settings.BaseCurrency);
                    }
                    else
                    {
                        expense += tx.Entries
                            .Where(e => accountById.TryGetValue(e.AccountId, out var a) && a.Type == AccountType.Assets && e.Direction == EntryDirection.Credit)
                            .Sum(e => e.Amount.Amount * _data.GetRate(e.Amount.CurrencyCode, _settings.BaseCurrency));
                    }
                }

                decimal income = 0;
                foreach (var tx in txList)
                {
                    var incEntry = tx.Entries.FirstOrDefault(e =>
                        accountById.TryGetValue(e.AccountId, out var a) && a.Type == AccountType.Income && e.Direction == EntryDirection.Credit);
                    if (incEntry != null)
                    {
                        income += incEntry.Amount.Amount * _data.GetRate(incEntry.Amount.CurrencyCode, _settings.BaseCurrency);
                    }
                    else
                    {
                        income += tx.Entries
                            .Where(e => accountById.TryGetValue(e.AccountId, out var a) && a.Type == AccountType.Assets && e.Direction == EntryDirection.Debit)
                            .Sum(e => e.Amount.Amount * _data.GetRate(e.Amount.CurrencyCode, _settings.BaseCurrency));
                    }
                }

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
            out Axis[] YAxes)   // диаграмма месяцев
        {
            MonthlySeries.Clear();
            MonthlyLabels.Clear();

            foreach (var r in MonthlyRows) MonthlyLabels.Add(r.Month);

            var incomeValues = MonthlyRows.Select(r => r.Income).ToArray();
            var expenseValues = MonthlyRows.Select(r => r.Expense).ToArray();

            var incomeGreen = SkiaSharp.SKColor.Parse("#00E676");
            MonthlySeries.Add(new LineSeries<decimal>
            {
                Name = "Доходы",
                Values = incomeValues,
                Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(incomeGreen, 2),
                Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(incomeGreen.WithAlpha(60)),
                GeometrySize = 8,
                GeometryStroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(incomeGreen, 2),
                GeometryFill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(incomeGreen),
                LineSmoothness = 0
            });

            var expenseRed = SkiaSharp.SKColor.Parse("#FF5252");
            MonthlySeries.Add(new LineSeries<decimal>
            {
                Name = "Расходы",
                Values = expenseValues,
                Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(expenseRed, 2),
                Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(expenseRed.WithAlpha(60)),
                GeometrySize = 8,
                GeometryStroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(expenseRed, 2),
                GeometryFill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(expenseRed),
                LineSmoothness = 0
            });

            var gridColor = SkiaSharp.SKColor.Parse("#222845");
            var labelColor = SkiaSharp.SKColor.Parse("#9E9E9E");

            XAxes = new[]
            {
                new Axis
                {
                    Labels = MonthlyLabels.ToArray(),
                    LabelsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(labelColor),
                    SeparatorsPaint = null
                }
            };
            YAxes = new[]
            {
                new Axis
                {
                    LabelsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(labelColor),
                    SeparatorsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(gridColor, 1)
                }
            };
        }
    }
}