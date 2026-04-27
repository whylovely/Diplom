// DTO серверных отчётов (ReportController). Клиент свои отчёты строит локально через
// TransactionAggregator и эти DTO не использует — они для админ-панели и будущей веб-версии.
namespace Shared.Reports;

/// <summary>Сумма по одной категории — строка таблицы или сегмент пирога.</summary>
public sealed record CategoryTotalDto(Guid? CategoryId, string CategoryName, decimal Total);

/// <summary>Месячные итоги: доходы и расходы в базовой валюте.</summary>
public sealed record MonthlyTotalDto(int Year, int Month, decimal Income, decimal Expense);

/// <summary>Обороты по счёту в разрезе Debit/Credit за период.</summary>
public sealed record AccountTurnoverDto(
    Guid AccountId, string AccountName, string Currency,
    decimal Debit, decimal Credit);

/// <summary>Сводный отчёт: общие итоги + разбивка по категориям расходов и доходов.</summary>
public sealed record SummaryDto(
    decimal TotalIncome, decimal TotalExpense, decimal Net,
    IReadOnlyList<CategoryTotalDto> ExpenseByCategory,
    IReadOnlyList<CategoryTotalDto> IncomeByCategory);
