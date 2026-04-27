namespace Server.Entities;

/// <summary>
/// Категория пользователя на сервере. Kind (доход/расход) на сервере не хранится —
/// при синхронизации обратно клиент восстанавливает Kind по списку «известных доходных»
/// названий в DtoMapper (хак, нормально работает для русского интерфейса).
/// </summary>
public sealed class CategoryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = default!;

    public string Name { get; set; } = default!;

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}