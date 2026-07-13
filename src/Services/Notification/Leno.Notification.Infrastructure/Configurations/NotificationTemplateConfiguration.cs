using System.Text.Json;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Notification.Infrastructure.Configurations;

/// <summary>
/// NotificationTemplate 通知模板的 EF Core 映射配置（snake_case）。
/// 变量列表序列化为 JSON 列。
/// </summary>
public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(128).IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(t => t.Channel).HasColumnName("channel").HasConversion<int>();
        builder.Property(t => t.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Body).HasColumnName("body").HasMaxLength(2000).IsRequired();
        builder.Property(t => t.SmsTemplateCode).HasColumnName("sms_template_code").HasMaxLength(64);
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(512);
        builder.Property(t => t.OperatorId).HasColumnName("operator_id");
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(t => t.Variables)
            .HasColumnName("variables")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<TemplateVariable>>(v, JsonOptions) ?? new List<TemplateVariable>());

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(t => new { t.Code, t.Channel }).HasDatabaseName("ix_notification_templates_code_channel");
    }
}