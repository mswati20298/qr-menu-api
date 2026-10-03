using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class ScanLogConfiguration : IEntityTypeConfiguration<ScanLog>
{
    public void Configure(EntityTypeBuilder<ScanLog> builder)
    {
        builder.ToTable("ScanLogs");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TableNumber).HasMaxLength(20);
        builder.Property(s => s.ScannedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(s => s.RestaurantId);
        builder.HasIndex(s => s.ScannedAt);
    }
}
