using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.ToTable("Restaurants");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Slug).HasMaxLength(100).IsRequired();
        builder.HasIndex(r => r.Slug).IsUnique();
        builder.Property(r => r.Tagline).HasMaxLength(300);
        builder.Property(r => r.Address).HasMaxLength(500);
        builder.Property(r => r.Phone).HasMaxLength(20);
        builder.Property(r => r.WhatsAppNumber).HasMaxLength(20).IsRequired();
        builder.Property(r => r.LogoUrl).HasMaxLength(500);
        builder.Property(r => r.CoverImageUrl).HasMaxLength(500);
        builder.Property(r => r.IsActive).HasDefaultValue(true);
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(r => r.GstPercentage).HasColumnType("decimal(5,2)");
        builder.Property(r => r.ServiceChargePercentage).HasColumnType("decimal(5,2)");
        builder.Property(r => r.WelcomeMessage).HasMaxLength(300);

        builder.HasMany(r => r.Users).WithOne(u => u.Restaurant).HasForeignKey(u => u.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Categories).WithOne(c => c.Restaurant).HasForeignKey(c => c.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.ScanLogs).WithOne(s => s.Restaurant).HasForeignKey(s => s.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Tables).WithOne(t => t.Restaurant).HasForeignKey(t => t.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Orders).WithOne(o => o.Restaurant).HasForeignKey(o => o.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(r => r.ThemeColor).HasMaxLength(20).IsRequired().HasDefaultValue(ThemeColors.Default);
        builder.HasMany(r => r.Backgrounds).WithOne(b => b.Restaurant).HasForeignKey(b => b.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.ServiceRequests).WithOne(s => s.Restaurant).HasForeignKey(s => s.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(r => r.Plan).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.PlanName).HasMaxLength(100);
        builder.Property(r => r.GstNumber).HasMaxLength(15);
        builder.Property(r => r.InvoicePrefix).HasMaxLength(10).IsRequired().HasDefaultValue("INV");
        builder.Property(r => r.UpiId).HasMaxLength(100);
        builder.Property(r => r.UpiPayeeName).HasMaxLength(100);
        builder.Property(r => r.KitchenPinHash).HasMaxLength(200);
        builder.HasOne<PricingPlan>().WithMany().HasForeignKey(r => r.PricingPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.PlanExpiresAt);
        builder.HasMany(r => r.SubscriptionEvents).WithOne(e => e.Restaurant).HasForeignKey(e => e.RestaurantId).OnDelete(DeleteBehavior.Cascade);
    }
}
