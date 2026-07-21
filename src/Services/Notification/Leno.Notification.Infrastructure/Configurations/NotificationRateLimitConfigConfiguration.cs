using Leno.Notification.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Notification.Infrastructure.Configurations;

/// <summary>
/// NotificationRateLimitConfig 限流配置的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class NotificationRateLimitConfigConfiguration : IEntityTypeConfiguration<NotificationRateLimitConfig>
{
    public void Configure(EntityTypeBuilder<NotificationRateLimitConfig> builder)
    {
        builder.ToTable("notification_rate_limit_configs");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.Channel).HasColumnName("channel").HasConversion<int>();
        builder.Property(n => n.HourlyLimit).HasColumnName("hourly_limit");
        builder.Property(n => n.DailyLimit).HasColumnName("daily_limit");
        builder.Property(n => n.Enabled).HasColumnName("enabled");

        builder.Property(n => n.CreatedAt).HasColumnName("created_at");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");
        builder.Property(n => n.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(n => n.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(n => n.Channel).IsUnique().HasDatabaseName("ix_notification_rate_limit_configs_channel");
    }
}
