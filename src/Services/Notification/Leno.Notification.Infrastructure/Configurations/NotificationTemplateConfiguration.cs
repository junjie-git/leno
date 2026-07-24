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

        // 国际化预留扩展位：Culture 值对象以 string 列持久化（null = zh-CN 默认行为不变）。
        // 值转换在 NotificationTemplateCulture? ↔ string? 之间双向映射。
        builder.Property(t => t.Culture)
            .HasConversion(
                v => v != null ? v.Culture : null,
                v => v != null ? NotificationTemplateCulture.Create(v) : null)
            .HasColumnName("culture")
            .HasMaxLength(16);

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

        // 多租户预留扩展位（4.7）：tenant_id 列声明（nullable，默认 null = 全局数据）。
        // BaseDbContext.OnModelCreating 会统一为 ITenantEntity 实体配置此列 + 全局查询过滤器，
        // 此处显式声明以明确 NotificationTemplate 支持多租户扩展位，并追加索引便于按租户查询。
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired(false);
        builder.HasIndex(t => t.TenantId).HasDatabaseName("ix_notification_templates_tenant_id");

        // (Code, Channel) 唯一约束（筛选 culture IS NULL）：防止同一 code+channel 存在多个默认文化（null）模板，
        // 避免 EfCoreNotificationTemplateRepository.FirstOrDefaultAsync 返回不确定。
        // 筛选条件限定仅对 null culture 行生效，为多语言变体（非 null culture）让出空间。
        builder.HasIndex(t => new { t.Code, t.Channel })
            .IsUnique()
            .HasFilter("[culture] IS NULL")
            .HasDatabaseName("ix_notification_templates_code_channel");

        // (Code, Channel, Culture) 复合唯一约束（筛选 culture IS NOT NULL）：国际化预留扩展位，
        // DG-8 决策门通过后，同一 code+channel 可按 culture 维度创建多语言变体（zh-CN / en-US 等）。
        // 当前阶段无非 null culture 数据，索引存在但不影响现有行为。
        builder.HasIndex(t => new { t.Code, t.Channel, t.Culture })
            .IsUnique()
            .HasFilter("[culture] IS NOT NULL")
            .HasDatabaseName("uq_notification_templates_code_channel_culture");
    }
}