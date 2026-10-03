namespace QrMenu.Domain.Entities;

public class ItemVariant
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }

    public MenuItem MenuItem { get; set; } = null!;
}
