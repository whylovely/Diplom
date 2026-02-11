namespace Server.Entities;

public enum EntryDirection
{
    Debit = 1,
    Credit = 2
}

public sealed class EntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = default!;

    public Guid TransactionId { get; set; }
    public TransactionEntity Transaction { get; set; } = default!;

    public Guid AccountId { get; set; }
    public AccountEntity Account { get; set; } = default!;

    public Guid? CategoryId { get; set; }
    public CategoryEntity? Category { get; set; }

    public int Direction { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
}