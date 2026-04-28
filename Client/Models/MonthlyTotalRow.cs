namespace Client.Models
{
    public sealed class MonthlyTotalRow
    {
        public string Month { get; set; } = "";
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Net => Income - Expense;
    }
}