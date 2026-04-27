using System;

namespace Client.Models
{
    /// <summary>
    /// Направление проводки в системе двойной записи.
    /// Для Asset-счёта Debit = увеличение баланса, Credit = уменьшение.
    /// Для Income-счёта (доходы) направления инвертированы — это бухгалтерская логика.
    /// </summary>
    public enum EntryDirection
    {
        Debit = 0,
        Credit = 1
    }

    /// <summary>
    /// Одна проводка двойной записи: счёт, направление, сумма.
    /// CategoryId опционален — у переводов и сторно его нет.
    /// </summary>
    public sealed class Entry
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid AccountId { get; set; }
        public Guid? CategoryId { get; set; }
        public EntryDirection Direction { get; set; }
        public required Money Amount { get; set; }
    }
}