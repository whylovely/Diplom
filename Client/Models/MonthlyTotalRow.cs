namespace Client.Models
{
    /// <summary>
    /// Итог одного месяца для отчётов и графика на дашборде.
    /// Month в формате «YYYY-MM», все суммы в базовой валюте пользователя.
    /// </summary>
    public sealed class MonthlyTotalRow
    {
        public string Month { get; set; } = "";
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Net => Income - Expense;
    }
}