using Leno.Notification.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Notification.Infrastructure.Configurations;

/// <summary>
/// NotificationRecord 通知记录的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    {
        builder.ToTable("notification_records");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.UserId).HasColumnName("user_id");
        builder.Property(n => n.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
        builder.Property(n => n.EventId).HasColumnName("event_id");
        builder.Property(n => n.Channel).HasColumnName("channel").HasConversion<int>();
        builder.Property(n => n.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(n => n.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
        builder.Property(n => n.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(n => n.RetryCount).HasColumnName("retry_count");
        builder.Property(n => n.IsRead).HasColumnName("is_read");
        builder.Property(n => n.SentAt).HasColumnName("sent_at");
        builder.Property(n => n.FailReason).HasColumnName("fail_reason").HasMaxLength(500);

        builder.Property(n => n.Version).HasColumnName("version").IsRowVersion();
        builder.Property(n => n.CreatedAt).HasColumnName("created_at");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");
        builder.Property(n => n.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(n => n.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(n => n.UserId).HasDatabaseName("ix_notification_records_user_id");
        builder.HasIndex(n => n.Status).HasDatabaseName("ix_notification_records_status");
        builder.HasIndex(n => n.EventId).HasDatabaseName("ix_notification_records_event_id");
    }
}
