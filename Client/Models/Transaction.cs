using System;
using System.Collections.Generic;

namespace Client.Models
{
    public sealed class Transaction
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
        public string Description { get; set; } = string.Empty;
        public List<Entry> Entries { get; set; } = new();
    }

    public enum TXKind
    {
        Expense = 1,
        Income = 2,
        Transfer = 3
    }

    public enum TxKindChoice
    {
        None,
        Expense,
        Income,
        Transfer,
        DebtRepayment, // отдал деньги
        DebtReceive // получил деньги
    }
}