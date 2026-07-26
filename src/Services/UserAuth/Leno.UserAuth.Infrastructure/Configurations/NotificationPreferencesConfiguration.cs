using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.UserAuth.Infrastructure.Configurations;

/// <summary>
/// NotificationPreferences 聚合根的 EF Core 映射配置。表名 snake_case；UserId 唯一索引。
/// 偏好项作为 owned collection 落 notification_preference_items 表。
/// </summary>
public sealed class NotificationPreferencesConfiguration : IEntityTypeConfiguration<NotificationPreferences>
{
    public void Configure(EntityTypeBuilder<NotificationPreferences> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.UserId).HasColumnName("user_id");
        builder.Property(p => p.DndEnabled).HasColumnName("dnd_enabled");
        builder.Property(p => p.DndStart).HasColumnName("dnd_start");
        builder.Property(p => p.DndEnd).HasColumnName("dnd_end");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 偏好项集合：owned collection，落 notification_preference_items 表
        builder.OwnsMany(p => p.Items, owned =>
        {
            owned.ToTable("notification_preference_items");
            owned.HasKey("NotificationPreferencesId", nameof(NotificationPreferenceItem.EventType));
            owned.WithOwner().HasForeignKey("NotificationPreferencesId");
            owned.Property<Guid>("NotificationPreferencesId").HasColumnName("notification_preferences_id");
            owned.Property(i => i.EventType).HasColumnName("event_type").HasConversion<int>();
            owned.Property(i => i.InAppEnabled).HasColumnName("in_app_enabled");
            owned.Property(i => i.SmsEnabled).HasColumnName("sms_enabled");
            owned.Property(i => i.EmailEnabled).HasColumnName("email_enabled");
        });

        builder.HasIndex(p => p.UserId).HasDatabaseName("ix_notification_preferences_user_id").IsUnique();
    }
}
