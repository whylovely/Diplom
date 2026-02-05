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

namespace Client.ViewModels.OperationWithReport
{
    public partial class DropReport
    {
        public static string BuildCSVReport(
            DateTimeOffset DateFrom,
            DateTimeOffset DateTo,
            decimal TotalIncome,
            decimal TotalExpense,
            decimal Net,
            ObservableCollection<CategoryTotalRow> ExpenseRows,
            ObservableCollection<CategoryTotalRow> IncomeRows,
            ObservableCollection<MonthlyTotalRow> MonthlyRows,
            ObservableCollection<AccountTurnoverRow> AccountRows,
            DateTimeOffset BalanceDate,
            ObservableCollection<AccountBalanceRow> BalanceRows
            )
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
    }
}
