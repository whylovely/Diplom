using System.ComponentModel.DataAnnotations;

namespace Server.Entities;

public sealed class CategoryEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string Name { get; set; } = "";
}