using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.Price).HasColumnType("decimal(10,2)");
        builder.Property(i => i.ImageUrl).HasMaxLength(500);
        builder.Property(i => i.Tag).HasMaxLength(50);
        builder.Property(i => i.IsAvailable).HasDefaultValue(true);
        builder.HasIndex(i => i.CategoryId);

        builder.HasMany(i => i.Variants).WithOne(v => v.MenuItem).HasForeignKey(v => v.MenuItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.AddOns).WithOne(a => a.MenuItem).HasForeignKey(a => a.MenuItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
