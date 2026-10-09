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

/// <summary>What creating and updating a dish have in common (one set of validation rules for both).</summary>
public interface IItemInput
{
    Guid CategoryId { get; }
    string Name { get; }
    string? Description { get; }
    decimal Price { get; }
    string? ImageUrl { get; }
    string? Tag { get; }
    List<VariantInput>? Variants { get; }
    List<AddOnInput>? AddOns { get; }
}

public record CreateItemRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsVeg,
    string? Tag,
    List<VariantInput>? Variants,
    List<AddOnInput>? AddOns) : IItemInput;

public record UpdateItemRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsVeg,
    string? Tag,
    List<VariantInput>? Variants,
    List<AddOnInput>? AddOns) : IItemInput;

public record UpdateAvailabilityRequest(bool IsAvailable);
