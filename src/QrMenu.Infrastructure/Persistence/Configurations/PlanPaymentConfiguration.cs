using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class PlanPaymentConfiguration : IEntityTypeConfiguration<PlanPayment>
{
    public void Configure(EntityTypeBuilder<PlanPayment> builder)
    {
        builder.ToTable("PlanPayments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PlanName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(10,2)");
        builder.Property(p => p.GatewayFee).HasColumnType("decimal(10,2)");
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.GatewayOrderId).HasMaxLength(100).IsRequired();
        builder.Property(p => p.GatewayPaymentId).HasMaxLength(100);
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(p => p.GatewayOrderId).IsUnique();
        builder.HasIndex(p => new { p.RestaurantId, p.CreatedAt });

        builder.HasOne(p => p.Restaurant).WithMany().HasForeignKey(p => p.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        // A plan that has been paid for can only be deactivated, never deleted.
        builder.HasOne(p => p.PricingPlan).WithMany().HasForeignKey(p => p.PricingPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
