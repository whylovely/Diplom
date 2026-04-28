using System;

namespace Client.Models
{
    /// <summary>
    /// Направление проводки в системе двойной записи.
    /// Для Asset-счёта Debit = увеличение баланса, Credit = уменьшение.
    /// Для Income-счёта направления инвертированы — это бухгалтерская логика.
    /// </summary>
    public enum EntryDirection
    {
        Debit = 0,
        Credit = 1
    }

    // Одна проводка двойной записи: счёт, направление, сумма
    public sealed class Entry
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid AccountId { get; set; }
        public Guid? CategoryId { get; set; }
        public EntryDirection Direction { get; set; }
        public required Money Amount { get; set; }
    }
}