using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class PaymentGatewayLogConfiguration : IEntityTypeConfiguration<PaymentGatewayLog>
{
    public void Configure(EntityTypeBuilder<PaymentGatewayLog> builder)
    {
        builder.ToTable("PaymentGatewayLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.GatewayOrderId).HasMaxLength(64);
        builder.Property(l => l.Kind).HasMaxLength(40).IsRequired();
        builder.Property(l => l.Note).HasMaxLength(500);
        builder.Property(l => l.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(l => new { l.GatewayOrderId, l.CreatedAt });
        builder.HasIndex(l => l.CreatedAt);
    }
}
