namespace QrMenu.Application.Items;

public record ItemVariantDto(Guid Id, string Name, decimal Price, bool IsDefault, int SortOrder);

public record ItemAddOnDto(Guid Id, string Name, decimal Price, int SortOrder);

public record MenuItemDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsVeg,
    string? Tag,
    bool IsAvailable,
    int SortOrder,
    List<ItemVariantDto> Variants,
    List<ItemAddOnDto> AddOns);

public record VariantInput(string Name, decimal Price, bool IsDefault);

public record AddOnInput(string Name, decimal Price);

public record CreateItemRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsVeg,
    string? Tag,
    List<VariantInput>? Variants,
    List<AddOnInput>? AddOns);

public record UpdateItemRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsVeg,
    string? Tag,
    List<VariantInput>? Variants,
    List<AddOnInput>? AddOns);

public record UpdateAvailabilityRequest(bool IsAvailable);
