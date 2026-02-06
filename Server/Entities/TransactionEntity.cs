using System.ComponentModel.DataAnnotations;

namespace Server.Entities;

public sealed class TransactionEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;

    public string Description { get; set; } = "";

    public List<EntryEntity> Entries { get; set; } = new();
}