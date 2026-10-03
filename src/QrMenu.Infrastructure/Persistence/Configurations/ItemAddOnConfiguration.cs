using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class ItemAddOnConfiguration : IEntityTypeConfiguration<ItemAddOn>
{
    public void Configure(EntityTypeBuilder<ItemAddOn> builder)
    {
        builder.ToTable("ItemAddOns");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Price).HasColumnType("decimal(10,2)");
        builder.HasIndex(a => a.MenuItemId);
    }
}
