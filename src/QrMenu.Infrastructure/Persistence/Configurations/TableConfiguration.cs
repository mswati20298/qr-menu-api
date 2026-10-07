using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class TableConfiguration : IEntityTypeConfiguration<Table>
{
    public void Configure(EntityTypeBuilder<Table> builder)
    {
        builder.ToTable("Tables");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Number).HasMaxLength(20).IsRequired();
        builder.Property(t => t.QrCode).HasMaxLength(16).IsRequired();
        builder.Property(t => t.IsActive).HasDefaultValue(true);
        builder.HasIndex(t => t.RestaurantId);
    }
}
