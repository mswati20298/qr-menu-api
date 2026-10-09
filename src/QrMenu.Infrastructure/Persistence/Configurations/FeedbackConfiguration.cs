using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.ToTable("Feedback");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Name).HasMaxLength(60).IsRequired();
        builder.Property(f => f.Comment).HasMaxLength(600);
        builder.Property(f => f.ImageUrl).HasMaxLength(500);
        builder.Property(f => f.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(f => f.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(f => f.Restaurant)
            .WithMany()
            .HasForeignKey(f => f.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        // One rating per order, and one QRenvo rating per restaurant (editing replaces it).
        // OrderId has no foreign key on purpose: orders already cascade from the restaurant, and SQL Server
        // refuses two cascade paths to the same table.
        builder.HasIndex(f => f.OrderId).IsUnique().HasFilter("[OrderId] IS NOT NULL");
        builder.HasIndex(f => f.RestaurantId).IsUnique().HasFilter("[Kind] = 'Owner'").HasDatabaseName("IX_Feedback_OwnerPerRestaurant");
        builder.HasIndex(f => new { f.RestaurantId, f.Kind, f.CreatedAt });
        builder.HasIndex(f => new { f.IsPublished, f.CreatedAt });
    }
}
