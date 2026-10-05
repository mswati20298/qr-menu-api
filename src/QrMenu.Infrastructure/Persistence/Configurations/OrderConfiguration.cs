using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.TableNumberSnapshot).HasMaxLength(20);
        builder.Property(o => o.CustomerName).HasMaxLength(200);
        builder.Property(o => o.CustomerPhone).HasMaxLength(20);
        builder.Property(o => o.Note).HasMaxLength(500);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Subtotal).HasColumnType("decimal(10,2)");
        builder.Property(o => o.ServiceChargeAmount).HasColumnType("decimal(10,2)");
        builder.Property(o => o.GstAmount).HasColumnType("decimal(10,2)");
        builder.Property(o => o.Total).HasColumnType("decimal(10,2)");
        builder.Property(o => o.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Source).HasConversion<string>().HasMaxLength(10);
        builder.Property(o => o.PaymentReference).HasMaxLength(50);
        builder.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(o => o.RestaurantId);
        builder.HasIndex(o => o.CreatedAt);
        builder.HasIndex(o => o.CustomerPhone);

        builder.HasOne(o => o.Table).WithMany(t => t.Orders).HasForeignKey(o => o.TableId).OnDelete(DeleteBehavior.ClientSetNull);
        // NoAction: an invoice is deleted only together with its restaurant, which deletes the orders too.
        builder.HasOne(o => o.Invoice).WithMany(i => i.Orders).HasForeignKey(o => o.InvoiceId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
