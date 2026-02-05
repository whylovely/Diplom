using Client.Services;
using Client.Models;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Kernel.Sketches;

namespace Client.ViewModels
{
    public sealed partial class ReportViewModel : ViewModelBase
    {
        private readonly IDataService _data;

        public event Action<string, string>? ExportCSVRequested;

        public ObservableCollection<CategoryTotalRow> ExpenseRows { get; } = new();
        public ObservableCollection<CategoryTotalRow> IncomeRows { get; } = new();
        public ObservableCollection<MonthlyTotalRow> MonthlyRows { get; } = new();
        public ObservableCollection<CategoryShareRow> ExpenseShareRows { get; } = new();
        public ObservableCollection<AccountTurnoverRow> AccountRows { get; } = new();
        public ObservableCollection<AccountBalanceRow> BalanceRows { get; } = new();

        public ObservableCollection<ISeries> ExpensePieSeries { get; } = new(); // Свойство графика по категориям (только расходы)

        public ObservableCollection<ISeries> MonthlySeries { get; } = new(); // Свойство графика по месяцам
        public ObservableCollection<string> MonthlyLabels { get; } = new();
        public Axis[] XAxes { get; private set; } = Array.Empty<Axis>();
        public Axis[] YAxes { get; private set; } = Array.Empty<Axis>();

        [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddMonths(-1);
        [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
        [ObservableProperty] private decimal _totalExpense;
        [ObservableProperty] private decimal _totalIncome;
        [ObservableProperty] private decimal _net;

        [ObservableProperty] private int _topN = 5; // количество показываемых категорий
        [ObservableProperty] private decimal _topExpensesSum;
        [ObservableProperty] private decimal _topExpensesShare;

        [ObservableProperty] private DateTimeOffset _balanceDate = DateTimeOffset.Now;  // Баланс на дату
        [ObservableProperty] private decimal _totalAssetsAtDate;

        public ReportViewModel(IDataService data)
        {
            _data = data;
            _data.DataChanged += () =>
            {
                Refresh();
            };

            _selectedSectionItem = SectionItems[0];
        }

        partial void OnTopNChanged(int value)
        {
            if (value < 1) TopN = 1;
            Refresh();
        }

        partial void OnBalanceDateChanged(DateTimeOffset Value) => RefreshBalance();

        private string BuildCSVReport()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"Период: {DateFrom:yyyy-MM-dd}-{DateTo:yyyy-MM-dd}");
            sb.AppendLine($"Итог доходов: {TotalIncome}");
            sb.AppendLine($"Итог расдоходов: {TotalExpense}");
            sb.AppendLine($"Итог: {Net}");
            sb.AppendLine();

            sb.AppendLine("Расходы по категориям");
            sb.AppendLine("Категория;Сумма");
            foreach (var r in ExpenseRows)
                sb.AppendLine($"{r.CategoryName};{r.Total}");
            sb.AppendLine();

            sb.AppendLine("Доходы по категориям");
            sb.AppendLine("Категория;Сумма");
            foreach (var r in IncomeRows)
                sb.AppendLine($"{r.CategoryName};{r.Total}");
            sb.AppendLine();

            sb.AppendLine("По месяцам");
            sb.AppendLine("Месяц;Доходы;Расходы;Итог");
            foreach (var r in MonthlyRows)
                sb.AppendLine($"{r.Month};{r.Income};{r.Expense};{r.Net}");
            sb.AppendLine();

            sb.AppendLine("Счета (Assets)");
            sb.AppendLine("Счет;Валюта;Доход;Расход;Изменения;Начало;Конец");
            foreach (var r in AccountRows)
                sb.AppendLine($"{r.AccountName};{r.CurrencyCode};{r.DebitTurnOver};{r.CreditTurnOver};{r.NetChange};{r.Opening};{r.Closing}");
            sb.AppendLine();

            sb.AppendLine("Баланс на дату");
            sb.AppendLine($"Дата: {BalanceDate:yyyy-MM-dd}");
            sb.AppendLine("Счет;Валюта;Остаток");
            foreach (var r in BalanceRows)
                sb.AppendLine($"{r.AccountName};{r.CurrencyCode};{r.Balance}");

            return sb.ToString();
        }

        public Array SectionValues => Enum.GetValues(typeof(ReportSelection));

        [ObservableProperty]
        private SectionItem _selectedSectionItem;

        public ReportSelection SelectedSection => SelectedSectionItem.Value;

        public bool IsSummary => SelectedSection == ReportSelection.Summary;
        public bool IsBalanceAtDate => SelectedSection == ReportSelection.BalanceAtDate;
        public bool IsMonthlyDynamics => SelectedSection == ReportSelection.MothlyDinamics;
        public bool IsAccountsTurnover => SelectedSection == ReportSelection.AccountsTurnover;
        public bool IsExpenseTop => SelectedSection == ReportSelection.ExpenseTop;
        public bool IsExpenseByCategory => SelectedSection == ReportSelection.ExpenseByCategory;
        public bool IsIncomeByCategory => SelectedSection == ReportSelection.IncomeByCategory;
        public sealed record SectionItem(ReportSelection Value, string Title);

        public string SelectedTitle => SelectedSectionItem.Title;

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
                    AccountName =  acc.Name,
                    CurrencyCode = acc.CurrencyCode,
                    Balance = bal
                });

                TotalAssetsAtDate = BalanceRows.Sum(r => r.Balance);
            }
        }

        [RelayCommand]
        private void ExportCSV()
        {
            var csv = BuildCSVReport();
            var fName = $"report_{DateFrom:yyMMdd}_{DateTo:yyMMdd}.csv";

            ExportCSVRequested?.Invoke(fName, csv);
        }

        [RelayCommand]
        public void Refresh()
        {
            ExpenseRows.Clear();
            IncomeRows.Clear();

            var txInRange = _data.Transactions
                .Where(t => t.Date >= DateFrom && t.Date <= DateTo)
                .ToList();  // Период

            var accountById = _data.Accounts.ToDictionary(a => a.Id);

            // Подсчет расходов
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

            foreach (var row in expenseGroups)
                ExpenseRows.Add(row);

            // Подсчет доходов
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

            foreach (var row in incomeGroups)
                IncomeRows.Add(row);

            _totalExpense = ExpenseRows.Sum(r => r.Total); 
            _totalIncome = IncomeRows.Sum(r => r.Total);
            _net = _totalIncome - _totalExpense;

            // Расчет помесячных итогов
            MonthlyRows.Clear();

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
               
            // Расчет графика по месяцам
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

            // Расчет долей расходов по категориям
            ExpenseShareRows.Clear();

            if (TotalExpense <=0)
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

            foreach (var row in top)
                ExpenseShareRows.Add(row);

            TopExpensesSum = top.Sum(r => r.Total);
            TopExpensesShare = Math.Round((TopExpensesSum / TotalExpense) * 100m, 2);

            // Построение графика расходов по категориям
            ExpensePieSeries.Clear();
            foreach (var r in ExpenseShareRows)
                ExpensePieSeries.Add(new PieSeries<decimal> { Values = new [] { r.Total }, Name = r.CategoryName });

            // Расчет остатков и оборотов
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

            RefreshBalance();
        }
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