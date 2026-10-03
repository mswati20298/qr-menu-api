namespace QrMenu.Application.Categories;

public record CategoryDto(Guid Id, string Name, int SortOrder, int ItemCount);

public record CreateCategoryRequest(string Name);

public record UpdateCategoryRequest(string Name);

public record ReorderRequest(List<Guid> OrderedIds);
