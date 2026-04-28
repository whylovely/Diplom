namespace Shared.Reports;

// DTO серверных отчётов
public sealed record CategoryTotalDto(Guid? CategoryId, string CategoryName, decimal Total);

public sealed record MonthlyTotalDto(int Year, int Month, decimal Income, decimal Expense);

public sealed record AccountTurnoverDto(
    Guid AccountId, string AccountName, string Currency,
    decimal Debit, decimal Credit);

public sealed record SummaryDto(
    decimal TotalIncome, decimal TotalExpense, decimal Net,
    IReadOnlyList<CategoryTotalDto> ExpenseByCategory,
    IReadOnlyList<CategoryTotalDto> IncomeByCategory);