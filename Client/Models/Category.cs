using System;

namespace Client.Models
{
    public sealed class Category
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;

        public CategoryKind Kind { get; set; } 
    }

    public enum CategoryKind
    {
        Expense,
        Income
    }
}