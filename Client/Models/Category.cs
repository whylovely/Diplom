using System;

namespace Client.Models
{
    public sealed class Category
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
    }
}