using System;
using System.Collections.Generic;

namespace Client.Models
{
    /// <summary>
    /// Финансовая операция = заголовок (дата, описание) + список проводок (Entry).
    /// Любая транзакция содержит минимум 2 проводки и должна быть сбалансирована:
    /// сумма Debit = сумма Credit. Это проверяется на сервере при PostTransaction.
    /// </summary>
    public sealed class Transaction
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
        public string Description { get; set; } = string.Empty;
        public List<Entry> Entries { get; set; } = new();
    }

    // Старый enum, оставлен для обратной совместимости. Новый код использует TxKindChoice.
    public enum TXKind
    {
        Expense = 1,
        Income = 2,
        Transfer = 3
    }

    /// <summary>
    /// Вид операции в форме «Новая транзакция». Определяет, какие поля видит пользователь
    /// и как <see cref="TransactionBuilder"/> построит проводки.
    /// </summary>
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