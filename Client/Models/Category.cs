using System;
using System.Collections.Generic;

namespace Client.Models
{
    public sealed class Category
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public CategoryKind Kind { get; set; } 
    }

    public enum CategoryKind { Expense, Income }

    public sealed class CategoryShareRow    // Доля категорий 
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal SharePercent { get; set; }
    }

    public sealed class DailyDetailRow  // день - категория
    {
        public string Date { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }

    public sealed class CategoryDetailGroup // Группировка по категории
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public List<DailyDetailRow> Days { get; set; } = new();
    }
}