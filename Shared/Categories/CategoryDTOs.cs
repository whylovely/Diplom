// DTO категорий. Поле Kind (доход/расход) намеренно не передаётся —
// сервер не отслеживает деление, клиент восстанавливает по имени в DtoMapper.
namespace Shared.Categories;

public sealed record CategoryDto(Guid Id, string Name);
public sealed record CreateCategoryRequest(string Name);
public sealed record UpdateCategoryRequest(string Name);