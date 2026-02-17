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
    public sealed partial class ReportViewModel : ViewModelBase
    {
        private readonly IDataService _data;

        public ReportViewModel(IDataService data)
        {
            _data = data;
            _data.DataChanged += () => Refresh();

            _selectedSectionItem = SectionItems[0];
        }

        partial void OnTopNChanged(int value)
        {
            if (value < 1) TopN = 1;
            Refresh();
        }

        [RelayCommand]
        public void Refresh()
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
        };

        [ObservableProperty] private SectionItem _selectedSectionItem;

        public string SelectedTitle => SelectedSectionItem.Title;
        public ReportSelection SelectedSection => SelectedSectionItem.Value;

        public bool IsSummary => SelectedSection == ReportSelection.Summary;
        public bool IsBalanceAtDate => SelectedSection == ReportSelection.BalanceAtDate;
        public bool IsMonthlyDynamics => SelectedSection == ReportSelection.MothlyDinamics;
        public bool IsAccountsTurnover => SelectedSection == ReportSelection.AccountsTurnover;
        public bool IsExpenseTop => SelectedSection == ReportSelection.ExpenseTop;
        public bool IsIncomeTop => SelectedSection == ReportSelection.IncomeTop;
        public bool IsExpenseByCategory => SelectedSection == ReportSelection.ExpenseByCategory;
        public bool IsIncomeByCategory => SelectedSection == ReportSelection.IncomeByCategory;

        partial void OnSelectedSectionItemChanged(SectionItem value)
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
        }
        //

        // Подсчет Расходов и Доходов
        [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddMonths(-1);
        [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
        [ObservableProperty] private decimal _totalExpense;
        [ObservableProperty] private decimal _net;
        public ObservableCollection<CategoryTotalRow> ExpenseRows { get; } = new();
        public ObservableCollection<CategoryTotalRow> IncomeRows { get; } = new();
        [ObservableProperty] private decimal _totalIncome;

        [RelayCommand] 
        public void RefreshSummary()
        {
            TotalExpense = ExpenseReport.RefreshExpenseRows(_data, DateFrom, DateTo, ExpenseRows);
            TotalIncome = IncomeReport.RefreshIncomeRows(_data, DateFrom, DateTo, IncomeRows);
            Net = TotalIncome - TotalExpense;
        }
        //

        // Группировка расходов по категориям с детализацией по дням
        public ObservableCollection<CategoryDetailGroup> ExpenseGroups { get; } = new();

        [RelayCommand]
        public void RefreshExpenseCategory() => ExpenseReport.RefreshExpenseGroups(_data, DateFrom, DateTo, ExpenseGroups);
        //

        // Группировка доходов по категориям с детализацией по дням
        public ObservableCollection<CategoryDetailGroup> IncomeGroups { get; } = new();

        [RelayCommand]
        public void RefreshIncomeCategory() => IncomeReport.RefreshIncomeGroups(_data, DateFrom, DateTo, IncomeGroups);
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
            MonthlyReport.RefreshMonthlyRows(_data, DateFrom, DateTo, MonthlyRows);
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
        partial void OnBalanceDateChanged(DateTimeOffset value) => RefreshBalance();
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

        [ObservableProperty] private ExportFormat _selectedExportFormat = ExportFormat.CSV;

        public ExportFormat[] ExportFormats { get; } = Enum.GetValues<ExportFormat>();

        [RelayCommand]
        private void Export()
        {
            var baseName = $"report_{DateFrom:yyMMdd}_{DateTo:yyMMdd}";

            switch (SelectedExportFormat)
            {
                case ExportFormat.CSV:
                    var csv = DropReport.BuildCSVReport(DateFrom, DateTo, TotalIncome, TotalExpense, Net, ExpenseRows, IncomeRows, MonthlyRows, AccountRows, BalanceDate, BalanceRows);
                    ExportRequested?.Invoke(baseName + ".csv", System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray());
                    break;

                case ExportFormat.TXT:
                    var txt = DropReport.BuildTXTReport(DateFrom, DateTo, TotalIncome, TotalExpense, Net, ExpenseRows, IncomeRows, MonthlyRows, AccountRows, BalanceDate, BalanceRows);
                    ExportRequested?.Invoke(baseName + ".txt", System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(txt)).ToArray());
                    break;

                case ExportFormat.Excel:
                    var xlsx = DropReport.BuildExcelReport(DateFrom, DateTo, TotalIncome, TotalExpense, Net, ExpenseRows, IncomeRows, MonthlyRows, AccountRows, BalanceDate, BalanceRows);
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
        IncomeByCategory
    }
}