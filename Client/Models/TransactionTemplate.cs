using System;

namespace Client.Models
{
    /// <summary>
    /// Сохранённый шаблон формы транзакции (например, «оплата интернета»).
    /// Все поля Id-ссылок (FromAccountId/ToAccountId/CategoryId) опциональны —
    /// шаблон может быть частичным, пользователь дозаполнит на форме.
    /// </summary>
    public sealed class TransactionTemplate
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public TxKindChoice Choice { get; set; }
        public Guid? FromAccountId { get; set; }
        public Guid? ToAccountId { get; set; }
        public Guid? CategoryId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
