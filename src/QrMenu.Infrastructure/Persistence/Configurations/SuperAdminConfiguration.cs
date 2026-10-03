using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class SuperAdminConfiguration : IEntityTypeConfiguration<SuperAdmin>
{
    public void Configure(EntityTypeBuilder<SuperAdmin> builder)
    {
        builder.ToTable("SuperAdmins");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Email).HasMaxLength(256).IsRequired();
        builder.Property(a => a.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(a => a.Email).IsUnique();
    }
}
