using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entities;

public enum EntryDirection
{
    Debit = 1,
    Credit = 2
}

public sealed class EntryEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [ForeignKey(nameof(Transaction))] public Guid TransactionId { get; set; }
    public TransactionEntity? Transaction { get; set; }

    public Guid AccountId { get; set; }
    public Guid CategoryId { get; set; }

    public EntryDirection Direction { get; set; }

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "RUB";
}