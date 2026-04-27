namespace Server.Entities;

/// <summary>
/// ВНИМАНИЕ: тут Debit=1, Credit=2 (а на клиенте Debit=0, Credit=1) — так сложилось исторически,
/// маппинг идёт через явный (int)cast в DtoMapper и контроллерах. При добавлении нового
/// направления нужно править оба места.
/// </summary>
public enum EntryDirection
{
    Debit = 1,
    Credit = 2
}

/// <summary>
/// Одна проводка двойной записи. UserId дублируется на проводке (а не только на транзакции)
/// для эффективной фильтрации и индексов (UserId, AccountId) / (UserId, CategoryId).
/// </summary>
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