namespace Shared.Categories;

public sealed record CategoryDto(Guid Id, string Name);
public sealed record CreateCategoryRequest(string Name);
public sealed record UpdateCategoryRequest(string Name);