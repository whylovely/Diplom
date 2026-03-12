using System;
using System.Collections.Generic;

namespace Client.Models
{
    public sealed class CalendarDay
    {
        public DateTime Date { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }

        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal Net => TotalIncome - TotalExpense;
        public bool HasTransactions => TotalIncome != 0 || TotalExpense != 0;

        public List<JournalRow> Transactions { get; set; } = new();

        public string DayNumber => Date.Day.ToString();
    }
}
