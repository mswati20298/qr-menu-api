using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Number).HasMaxLength(30).IsRequired();
        builder.Property(i => i.TableNumber).HasMaxLength(20);
        builder.Property(i => i.CustomerName).HasMaxLength(200);
        builder.Property(i => i.CustomerPhone).HasMaxLength(20);
        builder.Property(i => i.Subtotal).HasColumnType("decimal(10,2)");
        builder.Property(i => i.ServiceChargeAmount).HasColumnType("decimal(10,2)");
        builder.Property(i => i.GstAmount).HasColumnType("decimal(10,2)");
        builder.Property(i => i.Total).HasColumnType("decimal(10,2)");
        builder.Property(i => i.ServiceChargePercentage).HasColumnType("decimal(5,2)");
        builder.Property(i => i.GstPercentage).HasColumnType("decimal(5,2)");
        builder.Property(i => i.RestaurantName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.RestaurantAddress).HasMaxLength(500);
        builder.Property(i => i.RestaurantPhone).HasMaxLength(20);
        builder.Property(i => i.GstNumber).HasMaxLength(15);
        builder.Property(i => i.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // Guarantees no two invoices of a restaurant share a number, even if two are billed at once.
        builder.HasIndex(i => new { i.RestaurantId, i.Sequence }).IsUnique();
        builder.HasIndex(i => new { i.RestaurantId, i.CreatedAt });

        builder.HasOne(i => i.Restaurant).WithMany(r => r.Invoices).HasForeignKey(i => i.RestaurantId).OnDelete(DeleteBehavior.Cascade);
    }
}
