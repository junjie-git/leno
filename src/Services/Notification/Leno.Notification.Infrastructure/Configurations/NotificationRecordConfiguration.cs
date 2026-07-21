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
        builder.Property(n => n.TemplateCode).HasColumnName("template_code").HasMaxLength(128).IsRequired();
        builder.Property(n => n.EventId).HasColumnName("event_id");
        builder.Property(n => n.Channel).HasColumnName("channel").HasConversion<int>();
        builder.Property(n => n.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(n => n.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
        builder.Property(n => n.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(n => n.RetryCount).HasColumnName("retry_count");
        builder.Property(n => n.MaxRetry).HasColumnName("max_retry");
        builder.Property(n => n.NextRetryAt).HasColumnName("next_retry_at");
        builder.Property(n => n.IsRead).HasColumnName("is_read");
        builder.Property(n => n.SentAt).HasColumnName("sent_at");
        builder.Property(n => n.FailedAt).HasColumnName("failed_at");
        builder.Property(n => n.ErrorMessage).HasColumnName("error_message").HasMaxLength(500);
        builder.Property(n => n.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
        builder.Property(n => n.ContentSnapshot).HasColumnName("content_snapshot").HasColumnType("nvarchar(max)");
        builder.Property(n => n.ChannelMessageId).HasColumnName("channel_message_id").HasMaxLength(128);
        builder.Property(n => n.ChannelReceipt).HasColumnName("channel_receipt").HasColumnType("nvarchar(max)");
        builder.Property(n => n.BusinessRef).HasColumnName("business_ref").HasMaxLength(128);
        builder.Property(n => n.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128);

        builder.Property(n => n.CreatedAt).HasColumnName("created_at");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");
        builder.Property(n => n.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(n => n.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(n => n.UserId).HasDatabaseName("ix_notification_records_user_id");
        builder.HasIndex(n => n.Status).HasDatabaseName("ix_notification_records_status");
        builder.HasIndex(n => n.EventId).HasDatabaseName("ix_notification_records_event_id");
        builder.HasIndex(n => n.TemplateCode).HasDatabaseName("ix_notification_records_template_code");
        builder.HasIndex(n => n.IdempotencyKey)
            .IsUnique()
            .HasFilter("[idempotency_key] IS NOT NULL")
            .HasDatabaseName("ix_notification_records_idempotency_key");
    }
}