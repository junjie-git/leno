using System.Text.Json;
using Leno.Notification.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Notification.Infrastructure.Configurations;

/// <summary>
/// NotificationTemplate 通知模板的 EF Core 映射配置（snake_case）。
/// 变量列表序列化为 JSON 列。
/// </summary>
public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
        builder.Property(t => t.Channel).HasColumnName("channel").HasConversion<int>();
        builder.Property(t => t.TitleTemplate).HasColumnName("title_template").HasMaxLength(200).IsRequired();
        builder.Property(t => t.ContentTemplate).HasColumnName("content_template").HasMaxLength(2000).IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(t => t.Variables)
            .HasColumnName("variables")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        builder.Property(t => t.Version).HasColumnName("version").IsRowVersion();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(t => new { t.EventType, t.Channel }).HasDatabaseName("ix_notification_templates_event_channel");
    }
}
