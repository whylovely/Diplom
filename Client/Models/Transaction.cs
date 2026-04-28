using System;
using System.Collections.Generic;

namespace Client.Models
{
    // Финансовая операция = заголовок (дата, описание) + список проводок (Entry)
    public sealed class Transaction
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
        public string Description { get; set; } = string.Empty;
        public List<Entry> Entries { get; set; } = new();
    }

    public enum TxKindChoice
    {
        None,
        Expense,
        Income,
        Transfer,
        DebtRepayment, // погашение долга — я отдаю деньги
        DebtReceive    // получение долга — мне возвращают деньги
    }
}