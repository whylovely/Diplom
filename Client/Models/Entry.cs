using System;

namespace Client.Models
{
    public enum EntryDirection
    {
        Debit = 0,
        Credit = 1
    }

    public sealed class Entry   // проводка
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid AccountId { get; set; }
        public Guid? CategoryId { get; set; }
        public EntryDirection Direction { get; set; }
        public required Money Amount { get; set; }
    }
}