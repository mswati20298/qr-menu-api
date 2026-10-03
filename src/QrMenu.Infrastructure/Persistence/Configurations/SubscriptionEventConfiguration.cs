using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class SubscriptionEventConfiguration : IEntityTypeConfiguration<SubscriptionEvent>
{
    public void Configure(EntityTypeBuilder<SubscriptionEvent> builder)
    {
        builder.ToTable("SubscriptionEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Plan).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.PlanName).HasMaxLength(100);
        builder.Property(e => e.Amount).HasColumnType("decimal(10,2)");
        builder.Property(e => e.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.PaymentReference).HasMaxLength(100);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Property(e => e.PerformedBy).HasMaxLength(256).IsRequired();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => new { e.RestaurantId, e.CreatedAt });
    }
}
