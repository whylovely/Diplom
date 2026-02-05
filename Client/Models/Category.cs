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

    // Расчет процентов на категории
    public sealed class CategoryShareRow
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal SharePercent { get; set; }
    }

    public sealed class CategoryTotalRow
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }
}