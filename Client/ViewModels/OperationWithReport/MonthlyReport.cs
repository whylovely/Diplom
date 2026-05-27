using Client.Models;
using Client.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public partial class MonthlyReport
    {
        public static void RefreshMonthlyRows(
            IDataService data,
            SettingsService settings,
            System.DateTimeOffset? dateFrom,
            System.DateTimeOffset? dateTo,
            ObservableCollection<MonthlyTotalRow> monthlyRows)
        {
            monthlyRows.Clear();
            var accountById = data.Accounts.ToDictionary(a => a.Id);
            var rows = TransactionAggregator.BuildMonthlyTotals(
                data.Transactions, accountById,
                dateFrom, dateTo,
                (from, to) => data.GetRate(from, to),
                settings.BaseCurrency);

            foreach (var row in rows) monthlyRows.Add(row);
        }

        public static void RefreshMonthlyChart(
            ObservableCollection<MonthlyTotalRow> monthlyRows,
            ObservableCollection<ISeries> monthlySeries,
            ObservableCollection<string> monthlyLabels,
            out Axis[] xAxes,
            out Axis[] yAxes)
        {
            monthlySeries.Clear();
            monthlyLabels.Clear();

            foreach (var r in monthlyRows) monthlyLabels.Add(r.Month);

            var incomeGreen = SkiaSharp.SKColor.Parse("#059669");
            monthlySeries.Add(new LineSeries<decimal>
            {
                Name = "Доходы",
                Values = monthlyRows.Select(r => r.Income).ToArray(),
                Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(incomeGreen, 2),
                Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(incomeGreen.WithAlpha(30)),
                GeometrySize = 8,
                GeometryStroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(incomeGreen, 2),
                GeometryFill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(incomeGreen),
                LineSmoothness = 0
            });

            var expenseRed = SkiaSharp.SKColor.Parse("#DC2626");
            monthlySeries.Add(new LineSeries<decimal>
            {
                Name = "Расходы",
                Values = monthlyRows.Select(r => r.Expense).ToArray(),
                Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(expenseRed, 2),
                Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(expenseRed.WithAlpha(30)),
                GeometrySize = 8,
                GeometryStroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(expenseRed, 2),
                GeometryFill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(expenseRed),
                LineSmoothness = 0
            });

            var gridColor  = SkiaSharp.SKColor.Parse("#E5E7EB");
            var labelColor = SkiaSharp.SKColor.Parse("#6B7280");

            xAxes = new[]
            {
                new Axis
                {
                    Labels = monthlyLabels.ToArray(),
                    LabelsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(labelColor),
                    SeparatorsPaint = null
                }
            };
            yAxes = new[]
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