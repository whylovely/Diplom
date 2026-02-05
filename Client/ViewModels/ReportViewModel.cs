using Client.Models;
using Client.Services;
using Client.ViewModels.OperationWithReport;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using System;
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
            _data.DataChanged += () =>
            {
                Refresh();
            };

            _selectedSectionItem = SectionItems[0];
        }

        [ObservableProperty] private decimal _net;

        partial void OnTopNChanged(int value)
        {
            if (value < 1) TopN = 1;
            Refresh();
        }

        [RelayCommand]
        public void Refresh()
        {
            TotalExpense = RefreshExpenseRows();
            TotalIncome = RefreshIncomeRows();
            Net = TotalIncome - TotalExpense;
            RefreshMonthlyRows();
            RefreshMonthlyChart();
            RefreshExpenseChart();
            RefreshAccountsRows();
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
            OnPropertyChanged(nameof(IsExpenseByCategory));
            OnPropertyChanged(nameof(IsIncomeByCategory));
        }
        //

        // Подсчет Расходов
        [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddMonths(-1);
        [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
        [ObservableProperty] private decimal _totalExpense;
        public ObservableCollection<CategoryTotalRow> ExpenseRows { get; } = new();

        //[RelayCommand]
        private decimal RefreshExpenseRows()
        {
            ExpenseRows.Clear();
            var txInRange = _data.Transactions.Where(t => t.Date >= DateFrom && t.Date <= DateTo).ToList();
            var accountById = _data.Accounts.ToDictionary(a => a.Id);

            var expenseGroups = txInRange
                .SelectMany(t => t.Entries)
                .Where(e =>
                {
                    var acc = accountById[e.AccountId];
                    return acc.Type == AccountType.Expense && e.Direction == EntryDirection.Debit;
                })
                .GroupBy(e => e.CategoryId)
                .Select(g =>
                {
                    var catName = _data.Categories.FirstOrDefault(c => c.Id == g.Key).Name;
                    return new CategoryTotalRow
                    {
                        CategoryName = catName,
                        Total = g.Sum(x => x.Amount.Amount)
                    };
                })
                .OrderByDescending(r => r.Total);

            foreach (var row in expenseGroups) ExpenseRows.Add(row);

            return ExpenseRows.Sum(r => r.Total);
        }
        //

        // Подсчет Доходов
        public ObservableCollection<CategoryTotalRow> IncomeRows { get; } = new();
        [ObservableProperty] private decimal _totalIncome;

        //[RelayCommand]
        private decimal RefreshIncomeRows()
        {
            IncomeRows.Clear();
            var txInRange = _data.Transactions.Where(t => t.Date >= DateFrom && t.Date <= DateTo).ToList(); 
            var accountById = _data.Accounts.ToDictionary(a => a.Id);

            var incomeGroups = txInRange
                .SelectMany(t => t.Entries)
                .Where(e =>
                {
                    var acc = accountById[e.AccountId];
                    return acc.Type == AccountType.Income && e.Direction == EntryDirection.Credit;
                })
                .GroupBy(e => e.CategoryId)
                .Select(g =>
                {
                    var catName = _data.Categories.FirstOrDefault(c => c.Id == g.Key).Name;
                    return new CategoryTotalRow
                    {
                        CategoryName = catName,
                        Total = g.Sum(x => x.Amount.Amount)
                    };
                })
                .OrderByDescending(r => r.Total);

            foreach (var row in incomeGroups) IncomeRows.Add(row);

            return IncomeRows.Sum(r => r.Total);
        }
        //

        // Подсчет помесячных итогов
        public ObservableCollection<MonthlyTotalRow> MonthlyRows { get; } = new();

        [RelayCommand]
        private void RefreshMonthlyRows()
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
        //

        // Подсчет помесячного графика
        public ObservableCollection<ISeries> MonthlySeries { get; } = new();
        public ObservableCollection<string> MonthlyLabels { get; } = new();
        public Axis[] XAxes { get; private set; } = Array.Empty<Axis>();
        public Axis[] YAxes { get; private set; } = Array.Empty<Axis>();

        [RelayCommand]
        private void RefreshMonthlyChart()
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
        private void RefreshExpenseChart()
        {
            ExpenseShareRows.Clear();

            if (TotalExpense <= 0)
            {
                TopExpensesSum = 0;
                TopExpensesShare = 0;
                return;
            }

            var top = ExpenseRows
                .OrderByDescending(r => r.Total)
                .Take(TopN)
                .Select(r => new CategoryShareRow
                {
                    CategoryName = r.CategoryName,
                    Total = r.Total,
                    SharePercent = r.Total / TotalExpense
                }).ToList();

            foreach (var row in top) ExpenseShareRows.Add(row);

            TopExpensesSum = top.Sum(r => r.Total);
            TopExpensesShare = Math.Round((TopExpensesSum / TotalExpense) * 100m, 2);

            ExpensePieSeries.Clear();
            foreach (var r in ExpenseShareRows)
                ExpensePieSeries.Add(new PieSeries<decimal> { Values = new[] { r.Total }, Name = r.CategoryName });
        }
        //

        // Подсчет остатков и оборотов
        public ObservableCollection<AccountTurnoverRow> AccountRows { get; } = new();
        
        [RelayCommand]
        private void RefreshAccountsRows()
        {
            AccountRows.Clear();

            var assetAccounts = _data.Accounts
                .Where(a => a.Type == AccountType.Assets)
                .ToList();

            var allTx = _data.Transactions.ToList();

            foreach (var acc in assetAccounts)
            {
                var deltaBeforeFrom = allTx
                    .Where(t => t.Date < DateFrom)
                    .SelectMany(t => t.Entries)
                    .Where(e => e.AccountId == acc.Id)
                    .Sum(e => e.Direction == EntryDirection.Debit ? e.Amount.Amount : -e.Amount.Amount);

                var opening = acc.InitialBalance + deltaBeforeFrom;

                var entriesInPeriod = allTx
                    .Where(t => t.Date >= DateFrom && t.Date <= DateTo)
                    .SelectMany(t => t.Entries)
                    .Where(e => e.AccountId == acc.Id)
                    .ToList();

                var debitTurnover = entriesInPeriod
                    .Where(e => e.Direction == EntryDirection.Debit)
                    .Sum(e => e.Amount.Amount);

                var creditTurnover = entriesInPeriod
                    .Where(e => e.Direction == EntryDirection.Credit)
                    .Sum(e => e.Amount.Amount);

                var closing = opening + (debitTurnover - creditTurnover);

                AccountRows.Add(new AccountTurnoverRow
                {
                    AccountName = acc.Name,
                    CurrencyCode = acc.CurrencyCode,
                    Opening = opening,
                    DebitTurnOver = debitTurnover,
                    CreditTurnOver = creditTurnover,
                    Closing = closing
                });
            }
        }
        //

        // Обновление баланса на дату
        public ObservableCollection<AccountBalanceRow> BalanceRows { get; } = new();
        partial void OnBalanceDateChanged(DateTimeOffset Value) => RefreshBalance();
        [ObservableProperty] private DateTimeOffset _balanceDate = DateTimeOffset.Now;
        [ObservableProperty] private decimal _totalAssetsAtDate;

        [RelayCommand]
        public void RefreshBalance()
        {
            BalanceRows.Clear();

            var assetAccounts = _data.Accounts
                .Where(a => a.Type == AccountType.Assets)
                .ToList();

            var txUpToDate = _data.Transactions
                .Where(t => t.Date <= BalanceDate)
                .ToList();

            foreach (var acc in assetAccounts)
            {
                var d = txUpToDate
                    .SelectMany(t => t.Entries)
                    .Where(e => e.AccountId == acc.Id)
                    .Sum(e => e.Direction == EntryDirection.Debit ? e.Amount.Amount : -e.Amount.Amount);

                var bal = acc.InitialBalance + d;

                BalanceRows.Add(new AccountBalanceRow
                {
                    AccountName = acc.Name,
                    CurrencyCode = acc.CurrencyCode,
                    Balance = bal
                });

                TotalAssetsAtDate = BalanceRows.Sum(r => r.Balance);
            }
        }
        //

        // Формирование отчета на экспорт
        public event Action<string, string>? ExportCSVRequested;
        private string CSVReport() => DropReport.BuildCSVReport(DateFrom, DateTo, TotalIncome, TotalExpense, Net, ExpenseRows, IncomeRows, MonthlyRows, AccountRows, BalanceDate, BalanceRows);

        [RelayCommand]
        private void ExportCSV()
        {
            var csv = CSVReport();
            var fName = $"report_{DateFrom:yyMMdd}_{DateTo:yyMMdd}.csv";

            ExportCSVRequested?.Invoke(fName, csv);
        }
        //
    }

    public enum ReportSelection
    {
        Summary,
        BalanceAtDate,
        MothlyDinamics,
        AccountsTurnover,
        ExpenseTop, // сделать тоже самое для доходов
        ExpenseByCategory,
        IncomeByCategory
    }
}