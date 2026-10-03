using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(c => c.RestaurantId);

        builder.HasMany(c => c.MenuItems).WithOne(i => i.Category).HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}
