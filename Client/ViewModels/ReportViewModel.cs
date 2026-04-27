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
    /// <summary>
    /// Страница «Отчёты». Через переключатель <see cref="SelectedSection"/> показывает 9 разделов:
    /// сводный, баланс на дату, помесячная динамика, обороты по счетам, топ расходов/доходов,
    /// расходы/доходы по категориям с детализацией, календарь.
    /// Тяжёлая логика вынесена в статические классы <c>OperationWithReport/*</c>,
    /// которые делегируют чистую агрегацию <see cref="TransactionAggregator"/>.
    /// Поддерживает экспорт в CSV, TXT и Excel через <see cref="ExportRequested"/>.
    /// </summary>
    public sealed partial class ReportViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly INotificationService _notify;
        private readonly SettingsService _settings;
        private bool _isRefreshing;

        public ReportViewModel(IDataService data, INotificationService notify, SettingsService settings)
        {
            _data = data;
            _notify = notify;
            _settings = settings;

            CalendarVm = new CalendarViewModel(data, notify);

            _data.DataChanged += () => Refresh();
            _settings.SettingsChanged += () => Refresh();
            
            SelectedSectionItem = SectionItems[0];
            Refresh();
        }

        partial void OnTopNChanged(int oldValue, int newValue)
        {
            if (newValue < 1) TopN = 1;
            Refresh();
        }

        [RelayCommand]
        public void Refresh()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            try
            {
                RefreshSummary();
                RefreshMonthly();
                RefreshExpenseTop();
                RefreshIncomeTop();
                RefreshExpenseCategory();
                RefreshIncomeCategory();
                RefreshAccounts();
                RefreshBalance();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReportViewModel] Refresh error: {ex.Message}");
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        // Формирование заголовков в ComboBoxs
        public record SectionItem(ReportSelection Value, string Title);

        public SectionItem[] SectionItems { get; } =
{
            new(ReportSelection.Summary, "Сводный отчет"),
            new(ReportSelection.BalanceAtDate, "Баланс на дату"),
            new(ReportSelection.MothlyDinamics, "Помесячная динамика"),
            new(ReportSelection.AccountsTurnover, "Обороты по счетам"),
            new(ReportSelection.ExpenseTop, "Топ расходов по категориям"),
            new(ReportSelection.IncomeTop, "Топ доходов по категориям"),
            new(ReportSelection.ExpenseByCategory, "Расходы по категориям"),
            new(ReportSelection.IncomeByCategory, "Доходы по категориям"),
            new(ReportSelection.Calendar, "Календарь операций"),
        };

        [ObservableProperty] private SectionItem _selectedSectionItem;

        public string SelectedTitle => SelectedSectionItem.Title;
        public ReportSelection SelectedSection => SelectedSectionItem.Value;

        public bool IsSummary => SelectedSectionItem?.Value == ReportSelection.Summary;
        public bool IsBalanceAtDate => SelectedSectionItem?.Value == ReportSelection.BalanceAtDate;
        public bool IsMonthlyDynamics => SelectedSectionItem?.Value == ReportSelection.MothlyDinamics;
        public bool IsAccountsTurnover => SelectedSectionItem?.Value == ReportSelection.AccountsTurnover;
        public bool IsExpenseTop => SelectedSectionItem?.Value == ReportSelection.ExpenseTop;
        public bool IsIncomeTop => SelectedSectionItem?.Value == ReportSelection.IncomeTop;
        public bool IsExpenseByCategory => SelectedSectionItem?.Value == ReportSelection.ExpenseByCategory;
        public bool IsIncomeByCategory => SelectedSectionItem?.Value == ReportSelection.IncomeByCategory;
        public bool IsCalendar => SelectedSectionItem?.Value == ReportSelection.Calendar;

        public CalendarViewModel CalendarVm { get; }
        public string BaseCurrencyCode => _settings.BaseCurrency;

        partial void OnSelectedSectionItemChanged(SectionItem? oldValue, SectionItem newValue)
        {
            OnPropertyChanged(nameof(SelectedTitle));
            OnPropertyChanged(nameof(SelectedSection));
            OnPropertyChanged(nameof(IsSummary));
            OnPropertyChanged(nameof(IsBalanceAtDate));
            OnPropertyChanged(nameof(IsMonthlyDynamics));
            OnPropertyChanged(nameof(IsAccountsTurnover));
            OnPropertyChanged(nameof(IsExpenseTop));
            OnPropertyChanged(nameof(IsIncomeTop));
            OnPropertyChanged(nameof(IsExpenseByCategory));
            OnPropertyChanged(nameof(IsIncomeByCategory));
            OnPropertyChanged(nameof(IsCalendar));
        }
        //

        // Подсчет Расходов и Доходов
        [ObservableProperty] private DateTimeOffset? _dateFrom = DateTimeOffset.Now.AddMonths(-1);
        partial void OnDateFromChanged(DateTimeOffset? oldValue, DateTimeOffset? newValue) => Refresh();

        [ObservableProperty] private DateTimeOffset? _dateTo = DateTimeOffset.Now;
        partial void OnDateToChanged(DateTimeOffset? oldValue, DateTimeOffset? newValue) => Refresh();


        [ObservableProperty] private decimal _totalExpense;
        [ObservableProperty] private decimal _net;
        public ObservableCollection<CategoryShareRow> ExpenseRows { get; } = new();
        public ObservableCollection<CategoryShareRow> IncomeRows { get; } = new();
        [ObservableProperty] private decimal _totalIncome;

        [RelayCommand]
        public void RefreshSummary()
        {
            TotalExpense = ExpenseReport.RefreshExpenseRows(_data, _settings, DateFrom, DateTo, ExpenseRows);
            TotalIncome = IncomeReport.RefreshIncomeRows(_data, _settings, DateFrom, DateTo, IncomeRows);
            Net = TotalIncome - TotalExpense;
        }
        //

        // Группировка расходов по категориям с детализацией по дням
        public ObservableCollection<CategoryDetailGroup> ExpenseGroups { get; } = new();

        [RelayCommand]
        public void RefreshExpenseCategory() => ExpenseReport.RefreshExpenseGroups(_data, _settings, DateFrom, DateTo, ExpenseGroups);
        //

        // Группировка доходов по категориям с детализацией по дням
        public ObservableCollection<CategoryDetailGroup> IncomeGroups { get; } = new();

        [RelayCommand]
        public void RefreshIncomeCategory() => IncomeReport.RefreshIncomeGroups(_data, _settings, DateFrom, DateTo, IncomeGroups);
        //

        // Подсчет помесячных итогов
        public ObservableCollection<ISeries> MonthlySeries { get; } = new();
        public ObservableCollection<string> MonthlyLabels { get; } = new();
        public Axis[] XAxes { get; private set; } = Array.Empty<Axis>();
        public Axis[] YAxes { get; private set; } = Array.Empty<Axis>();
        public ObservableCollection<MonthlyTotalRow> MonthlyRows { get; } = new();

        [RelayCommand]
        public void RefreshMonthly()
        {
            MonthlyReport.RefreshMonthlyRows(_data, _settings, DateFrom, DateTo, MonthlyRows);
            MonthlyReport.RefreshMonthlyChart(MonthlyRows, MonthlySeries, MonthlyLabels, out var xAxes, out var yAxes);
            XAxes = xAxes;
            YAxes = yAxes;
            OnPropertyChanged(nameof(XAxes));
            OnPropertyChanged(nameof(YAxes));
        }
        //

        // Подсчет графика по расходам
        public ObservableCollection<CategoryShareRow> ExpenseShareRows { get; } = new();
        public ObservableCollection<ISeries> ExpensePieSeries { get; } = new();
        [ObservableProperty] private int _topN = 5;
        [ObservableProperty] private decimal _topExpensesSum;
        [ObservableProperty] private decimal _topExpensesShare;

        [RelayCommand]
        public void RefreshExpenseTop() => ExpenseReport.RefreshExpenseChart(_data, DateFrom, DateTo, ExpenseShareRows, ExpensePieSeries, TotalExpense, TopN, TopExpensesSum, TopExpensesShare, ExpenseRows);
        //

        // Подсчет графика по доходам
        public ObservableCollection<CategoryShareRow> IncomeShareRows { get; } = new();
        public ObservableCollection<ISeries> IncomePieSeries { get; } = new();
        [ObservableProperty] private decimal _topIncomesSum;
        [ObservableProperty] private decimal _topIncomesShare;

        [RelayCommand]
        public void RefreshIncomeTop() => IncomeReport.RefreshIncomeChart(_data, DateFrom, DateTo, IncomeShareRows, IncomePieSeries, TotalIncome, TopN, TopIncomesSum, TopIncomesShare, IncomeRows);
        //

        // Подсчет остатков и оборотов
        public ObservableCollection<AccountTurnoverRow> AccountRows { get; } = new();
        
        [RelayCommand]
        public void RefreshAccounts() => AccountReport.RefreshAccountsRows(_data, DateFrom, DateTo, AccountRows);
        //

        // Обновление баланса на дату
        public ObservableCollection<AccountBalanceRow> BalanceRows { get; } = new();
        partial void OnBalanceDateChanged(DateTimeOffset oldValue, DateTimeOffset newValue) => RefreshBalance();
        [ObservableProperty] private DateTimeOffset _balanceDate = DateTimeOffset.Now;
        [ObservableProperty] private decimal _totalAssetsAtDate;

        [RelayCommand]
        public void RefreshBalance()
        {
            TotalAssetsAtDate = BalanceReport.RefreshBalanceRows(_data, BalanceDate, BalanceRows);
        }
        //

        // Формирование отчета на экспорт
        public event Action<string, byte[]>? ExportRequested;

        [RelayCommand]
        private void Export(string formatStr)
        {
            if (!Enum.TryParse<ExportFormat>(formatStr, out var format)) return;
            if (!DateFrom.HasValue || !DateTo.HasValue) return;

            var baseName = $"report_{DateFrom.Value:yyMMdd}_{DateTo.Value:yyMMdd}";

            switch (format)
            {
                case ExportFormat.CSV:
                    var csv = DropReport.BuildCSVReport(DateFrom.Value, DateTo.Value, TotalIncome, TotalExpense, Net, ExpenseRows, IncomeRows, MonthlyRows, AccountRows, BalanceDate, BalanceRows);
                    ExportRequested?.Invoke(baseName + ".csv", System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray());
                    break;

                case ExportFormat.TXT:
                    var txt = DropReport.BuildTXTReport(DateFrom.Value, DateTo.Value, TotalIncome, TotalExpense, Net, ExpenseRows, IncomeRows, MonthlyRows, AccountRows, BalanceDate, BalanceRows);
                    ExportRequested?.Invoke(baseName + ".txt", System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(txt)).ToArray());
                    break;

                case ExportFormat.Excel:
                    var xlsx = DropReport.BuildExcelReport(DateFrom.Value, DateTo.Value, TotalIncome, TotalExpense, Net, ExpenseRows, IncomeRows, MonthlyRows, AccountRows, BalanceDate, BalanceRows);
                    ExportRequested?.Invoke(baseName + ".xlsx", xlsx);
                    break;
            }
        }
        //
    }

    public enum ExportFormat
    {
        CSV,
        TXT,
        Excel
    }

    public enum ReportSelection
    {
        Summary,
        BalanceAtDate,
        MothlyDinamics,
        AccountsTurnover,
        ExpenseTop,
        IncomeTop,
        ExpenseByCategory,
        IncomeByCategory,
        Calendar
    }
}