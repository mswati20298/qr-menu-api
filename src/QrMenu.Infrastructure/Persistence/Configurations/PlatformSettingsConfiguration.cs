using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Configurations;

public class PlatformSettingsConfiguration : IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> builder)
    {
        builder.ToTable("PlatformSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(s => s.RazorpayKeyId).HasMaxLength(64);
        builder.Property(s => s.RazorpayKeySecretEncrypted).HasMaxLength(512);
        builder.Property(s => s.RazorpayWebhookSecretEncrypted).HasMaxLength(512);
        builder.Property(s => s.GeminiApiKeyEncrypted).HasMaxLength(512);
    }
}
