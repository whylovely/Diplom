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
using System.IO;
using System.Text;
using ClosedXML.Excel;

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
            var sb = new StringBuilder();

            sb.AppendLine($"Период: {DateFrom:yyyy-MM-dd}-{DateTo:yyyy-MM-dd}");
            sb.AppendLine($"Итог доходов: {TotalIncome}");
            sb.AppendLine($"Итог расходов: {TotalExpense}");
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

        public static string BuildTXTReport(
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
            ObservableCollection<AccountBalanceRow> BalanceRows)
        {
            var sb = new StringBuilder();
            var sep = new string('═', 60);

            sb.AppendLine(sep);
            sb.AppendLine("                    ФИНАНСОВЫЙ ОТЧЁТ");
            sb.AppendLine(sep);
            sb.AppendLine($"  Период:   {DateFrom:dd.MM.yyyy} — {DateTo:dd.MM.yyyy}");
            sb.AppendLine($"  Доходы:   {TotalIncome:N2}");
            sb.AppendLine($"  Расходы:  {TotalExpense:N2}");
            sb.AppendLine($"  Итог:     {Net:N2}");
            sb.AppendLine(sep);
            sb.AppendLine();

            // Расходы по категориям
            sb.AppendLine("  РАСХОДЫ ПО КАТЕГОРИЯМ");
            sb.AppendLine($"  {"Категория",-30} {"Сумма",15}");
            sb.AppendLine("  " + new string('─', 47));
            foreach (var r in ExpenseRows)
                sb.AppendLine($"  {r.CategoryName,-30} {r.Total,15:N2}");
            sb.AppendLine();

            // Доходы по категориям
            sb.AppendLine("  ДОХОДЫ ПО КАТЕГОРИЯМ");
            sb.AppendLine($"  {"Категория",-30} {"Сумма",15}");
            sb.AppendLine("  " + new string('─', 47));
            foreach (var r in IncomeRows)
                sb.AppendLine($"  {r.CategoryName,-30} {r.Total,15:N2}");
            sb.AppendLine();

            // По месяцам
            sb.AppendLine("  ПОМЕСЯЧНАЯ ДИНАМИКА");
            sb.AppendLine($"  {"Месяц",-18} {"Доходы",12} {"Расходы",12} {"Итог",12}");
            sb.AppendLine("  " + new string('─', 56));
            foreach (var r in MonthlyRows)
                sb.AppendLine($"  {r.Month,-18} {r.Income,12:N2} {r.Expense,12:N2} {r.Net,12:N2}");
            sb.AppendLine();

            // Счета
            sb.AppendLine("  ОБОРОТЫ ПО СЧЕТАМ");
            sb.AppendLine($"  {"Счет",-20} {"Валюта",-6} {"Начало",12} {"Доходы",12} {"Расходы",12} {"Конец",12}");
            sb.AppendLine("  " + new string('─', 76));
            foreach (var r in AccountRows)
                sb.AppendLine($"  {r.AccountName,-20} {r.CurrencyCode,-6} {r.Opening,12:N2} {r.DebitTurnOver,12:N2} {r.CreditTurnOver,12:N2} {r.Closing,12:N2}");
            sb.AppendLine();

            // Баланс на дату
            sb.AppendLine($"  БАЛАНС НА {BalanceDate:dd.MM.yyyy}");
            sb.AppendLine($"  {"Счет",-30} {"Валюта",-6} {"Остаток",15}");
            sb.AppendLine("  " + new string('─', 53));
            foreach (var r in BalanceRows)
                sb.AppendLine($"  {r.AccountName,-30} {r.CurrencyCode,-6} {r.Balance,15:N2}");

            sb.AppendLine();
            sb.AppendLine(sep);

            return sb.ToString();
        }

        public static byte[] BuildExcelReport(
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
            ObservableCollection<AccountBalanceRow> BalanceRows)
        {
            using var wb = new XLWorkbook();

            var ws = wb.Worksheets.Add("Итог");
            ws.Cell("A1").Value = "Финансовый отчёт";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 14;
            ws.Cell("A3").Value = "Период";
            ws.Cell("B3").Value = $"{DateFrom:dd.MM.yyyy} — {DateTo:dd.MM.yyyy}";
            ws.Cell("A4").Value = "Доходы";
            ws.Cell("B4").Value = TotalIncome;
            ws.Cell("B4").Style.NumberFormat.Format = "#,##0.00";
            ws.Cell("A5").Value = "Расходы";
            ws.Cell("B5").Value = TotalExpense;
            ws.Cell("B5").Style.NumberFormat.Format = "#,##0.00";
            ws.Cell("A6").Value = "Итог";
            ws.Cell("B6").Value = Net;
            ws.Cell("B6").Style.Font.Bold = true;
            ws.Cell("B6").Style.NumberFormat.Format = "#,##0.00";
            ws.Columns().AdjustToContents();

            var wsExp = wb.Worksheets.Add("Расходы");
            wsExp.Cell("A1").Value = "Категория";
            wsExp.Cell("B1").Value = "Сумма";
            wsExp.Range("A1:B1").Style.Font.Bold = true;
            for (int i = 0; i < ExpenseRows.Count; i++)
            {
                wsExp.Cell(i + 2, 1).Value = ExpenseRows[i].CategoryName;
                wsExp.Cell(i + 2, 2).Value = ExpenseRows[i].Total;
                wsExp.Cell(i + 2, 2).Style.NumberFormat.Format = "#,##0.00";
            }
            wsExp.Columns().AdjustToContents();

            var wsInc = wb.Worksheets.Add("Доходы");
            wsInc.Cell("A1").Value = "Категория";
            wsInc.Cell("B1").Value = "Сумма";
            wsInc.Range("A1:B1").Style.Font.Bold = true;
            for (int i = 0; i < IncomeRows.Count; i++)
            {
                wsInc.Cell(i + 2, 1).Value = IncomeRows[i].CategoryName;
                wsInc.Cell(i + 2, 2).Value = IncomeRows[i].Total;
                wsInc.Cell(i + 2, 2).Style.NumberFormat.Format = "#,##0.00";
            }
            wsInc.Columns().AdjustToContents();

            var wsMon = wb.Worksheets.Add("По месяцам");
            wsMon.Cell("A1").Value = "Месяц";
            wsMon.Cell("B1").Value = "Доходы";
            wsMon.Cell("C1").Value = "Расходы";
            wsMon.Cell("D1").Value = "Итог";
            wsMon.Range("A1:D1").Style.Font.Bold = true;
            for (int i = 0; i < MonthlyRows.Count; i++)
            {
                wsMon.Cell(i + 2, 1).Value = MonthlyRows[i].Month;
                wsMon.Cell(i + 2, 2).Value = MonthlyRows[i].Income;
                wsMon.Cell(i + 2, 2).Style.NumberFormat.Format = "#,##0.00";
                wsMon.Cell(i + 2, 3).Value = MonthlyRows[i].Expense;
                wsMon.Cell(i + 2, 3).Style.NumberFormat.Format = "#,##0.00";
                wsMon.Cell(i + 2, 4).Value = MonthlyRows[i].Net;
                wsMon.Cell(i + 2, 4).Style.NumberFormat.Format = "#,##0.00";
            }
            wsMon.Columns().AdjustToContents();

            var wsAcc = wb.Worksheets.Add("Счета");
            wsAcc.Cell("A1").Value = "Счет";
            wsAcc.Cell("B1").Value = "Валюта";
            wsAcc.Cell("C1").Value = "Начало";
            wsAcc.Cell("D1").Value = "Доходы";
            wsAcc.Cell("E1").Value = "Расходы";
            wsAcc.Cell("F1").Value = "Конец";
            wsAcc.Range("A1:F1").Style.Font.Bold = true;
            for (int i = 0; i < AccountRows.Count; i++)
            {
                wsAcc.Cell(i + 2, 1).Value = AccountRows[i].AccountName;
                wsAcc.Cell(i + 2, 2).Value = AccountRows[i].CurrencyCode;
                wsAcc.Cell(i + 2, 3).Value = AccountRows[i].Opening;
                wsAcc.Cell(i + 2, 3).Style.NumberFormat.Format = "#,##0.00";
                wsAcc.Cell(i + 2, 4).Value = AccountRows[i].DebitTurnOver;
                wsAcc.Cell(i + 2, 4).Style.NumberFormat.Format = "#,##0.00";
                wsAcc.Cell(i + 2, 5).Value = AccountRows[i].CreditTurnOver;
                wsAcc.Cell(i + 2, 5).Style.NumberFormat.Format = "#,##0.00";
                wsAcc.Cell(i + 2, 6).Value = AccountRows[i].Closing;
                wsAcc.Cell(i + 2, 6).Style.NumberFormat.Format = "#,##0.00";
            }
            wsAcc.Columns().AdjustToContents();

            var wsBal = wb.Worksheets.Add("Баланс");
            wsBal.Cell("A1").Value = $"Баланс на {BalanceDate:dd.MM.yyyy}";
            wsBal.Cell("A1").Style.Font.Bold = true;
            wsBal.Cell("A3").Value = "Счет";
            wsBal.Cell("B3").Value = "Валюта";
            wsBal.Cell("C3").Value = "Остаток";
            wsBal.Range("A3:C3").Style.Font.Bold = true;
            for (int i = 0; i < BalanceRows.Count; i++)
            {
                wsBal.Cell(i + 4, 1).Value = BalanceRows[i].AccountName;
                wsBal.Cell(i + 4, 2).Value = BalanceRows[i].CurrencyCode;
                wsBal.Cell(i + 4, 3).Value = BalanceRows[i].Balance;
                wsBal.Cell(i + 4, 3).Style.NumberFormat.Format = "#,##0.00";
            }
            wsBal.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
