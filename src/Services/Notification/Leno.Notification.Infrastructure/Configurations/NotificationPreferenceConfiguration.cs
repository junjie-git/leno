using System.Text.Json;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Notification.Infrastructure.Configurations;

/// <summary>
/// NotificationPreference 通知偏好的 EF Core 映射配置（snake_case）。
/// 事件渠道字典序列化为 JSON 列。
/// </summary>
public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.UserId).HasColumnName("user_id");
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(p => p.EventChannels)
            .HasColumnName("event_channels")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, List<NotificationChannel>>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, List<NotificationChannel>>());

        builder.Property(p => p.Version).HasColumnName("version").IsRowVersion();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(p => p.UserId).IsUnique().HasDatabaseName("ix_notification_preferences_user_id");
    }
}
