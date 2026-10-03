using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class RestaurantBackgroundConfiguration : IEntityTypeConfiguration<RestaurantBackground>
{
    public void Configure(EntityTypeBuilder<RestaurantBackground> builder)
    {
        builder.ToTable("RestaurantBackgrounds");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(b => b.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(b => b.RestaurantId);
    }
}
