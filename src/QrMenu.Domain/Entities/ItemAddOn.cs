namespace QrMenu.Domain.Entities;

public class ItemAddOn
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int SortOrder { get; set; }

    public MenuItem MenuItem { get; set; } = null!;
}
