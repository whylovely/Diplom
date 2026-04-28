namespace Server.Entities;

// Заголовок транзакции. Сами проводки лежат в Entries
public sealed class TransactionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = default!;

    public DateTimeOffset Date { get; set; }
    public string? Description { get; set; }

    public List<EntryEntity> Entries { get; set; } = new();
}