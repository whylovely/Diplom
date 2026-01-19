using System;

namespace Client.Models
{
    public enum EntryDirection
    {
        Debit = 1,
        Credit = 2
    }

    public sealed class Entry
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid AccountId { get; set; }
        public Guid CategoryId { get; set; }
        public EntryDirection Direction { get; set; }
        public Money Amount { get; set; } // храним все в исходной валюте
    }
}