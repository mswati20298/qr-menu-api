using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("AdminAuditLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.AdminEmail).HasMaxLength(256).IsRequired();
        builder.Property(l => l.Action).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Path).HasMaxLength(300).IsRequired();
        builder.Property(l => l.TargetId).HasMaxLength(100);
        builder.Property(l => l.IpAddress).HasMaxLength(64);
        builder.HasIndex(l => l.CreatedAt);
    }
}
