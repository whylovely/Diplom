using System;

namespace Client.Models
{
    public sealed class JournalRow
    {
        public Guid TransactionId { get; set; }
        public DateTimeOffset Date { get; set; }
        public string Description { get; set; } = "";
        public string TypeLabel { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string? ToAccountName { get; set; }
        public string? CategoryName { get; set; }
        public decimal Amount { get; set; }           
        public string CurrencyCode { get; set; } = ""; 
        public bool IsExpense { get; set; }
        public bool IsIncome { get; set; }
        public bool IsTransfer { get; set; }
        public bool IsDuplicate { get; set; }

        public string FormattedAmount
        {
            get
            {
                var sign = IsExpense ? "−" : IsIncome ? "+" : "";
                return $"{sign}{Amount:N2} {CurrencyCode}";
            }
        }

        public string DateFormatted => Date.ToString("dd.MM.yyyy");
    }
}