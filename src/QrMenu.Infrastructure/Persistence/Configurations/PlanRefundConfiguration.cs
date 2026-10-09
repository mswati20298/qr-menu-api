using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class PlanRefundConfiguration : IEntityTypeConfiguration<PlanRefund>
{
    public void Configure(EntityTypeBuilder<PlanRefund> builder)
    {
        builder.ToTable("PlanRefunds");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.PlanName).HasMaxLength(100);
        builder.Property(r => r.PaymentAmount).HasPrecision(10, 2);
        builder.Property(r => r.Amount).HasPrecision(10, 2);
        builder.Property(r => r.Fee).HasPrecision(10, 2);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.RequestedBy).HasMaxLength(20);
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.AdminNote).HasMaxLength(500);
        builder.Property(r => r.DecidedBy).HasMaxLength(256);
        builder.Property(r => r.GatewayRefundId).HasMaxLength(64);
        builder.Property(r => r.RequestedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(r => r.Restaurant).WithMany().HasForeignKey(r => r.RestaurantId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.Status, r.RequestedAt });
        builder.HasIndex(r => r.PlanPaymentId);
        builder.HasIndex(r => r.PaymentEventId);
        builder.HasIndex(r => r.GatewayRefundId).IsUnique().HasFilter("[GatewayRefundId] IS NOT NULL");
    }
}
