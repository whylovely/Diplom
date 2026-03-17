using System;

namespace Client.Models
{
    public sealed class JournalRow
    {
        public Guid TransactionId { get; set; }
        public DateTimeOffset Date { get; set; }
        public string Description { get; set; } = "";
        public string TypeLabel { get; set; } = "";    // "Расход" / "Доход" / "Перевод"
        public string AccountName { get; set; } = "";  // имя счёта
        public string? ToAccountName { get; set; }     // имя счёта назначения (для переводов)
        public string? CategoryName { get; set; }      // имя категории (null для перевода)
        public decimal Amount { get; set; }           
        public string CurrencyCode { get; set; } = ""; 
        public bool IsExpense { get; set; }            // красный
        public bool IsIncome { get; set; }             // зелёный
        public bool IsTransfer { get; set; }           // синий
        public bool IsDuplicate { get; set; }          // возможный дубликат

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