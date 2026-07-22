using Leno.Notification.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Notification.Infrastructure.Configurations;

/// <summary>
/// NotificationConfig 渠道配置的 EF Core 映射配置（snake_case）。
/// (Channel, ConfigKey) 唯一约束确保同一渠道同一配置键仅一行。
/// </summary>
public sealed class NotificationConfigConfiguration : IEntityTypeConfiguration<NotificationConfig>
{
    public void Configure(EntityTypeBuilder<NotificationConfig> builder)
    {
        builder.ToTable("notification_configs");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Channel).HasColumnName("channel").HasConversion<int>();
        builder.Property(c => c.ConfigKey).HasColumnName("config_key").HasMaxLength(64).IsRequired();
        builder.Property(c => c.ConfigValue).HasColumnName("config_value").HasMaxLength(512).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(256);
        builder.Property(c => c.IsSensitive).HasColumnName("is_sensitive");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(c => new { c.Channel, c.ConfigKey })
            .IsUnique()
            .HasDatabaseName("ix_notification_configs_channel_key");
    }
}
