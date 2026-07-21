using System.Text.Json;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Promotion.Infrastructure.Configurations;

/// <summary>
/// PromotionActivity 聚合根的 EF Core 映射配置（snake_case）。
/// Rules（满减规则集合）序列化为 JSON 列，按值对象存储。
/// </summary>
public sealed class PromotionActivityConfiguration : IEntityTypeConfiguration<PromotionActivity>
{
    /// <summary>
    /// Rules JSON 列序列化/反序列化选项：snake_case 命名策略 + 大小写无关匹配。
    /// PropertyNameCaseInsensitive=true 用于向后兼容历史 PascalCase 数据（容忍大小写差异）。
    /// </summary>
    private static readonly JsonSerializerOptions RuleJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public void Configure(EntityTypeBuilder<PromotionActivity> builder)
    {
        builder.ToTable("promotion_activities");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(a => a.Type).HasColumnName("type").HasConversion<int>();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(a => a.StartTime).HasColumnName("start_time");
        builder.Property(a => a.EndTime).HasColumnName("end_time");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // Rules 满减规则集合序列化为 JSON 列，通过 backing field _rules 访问（DDD 封装）
        // Rules 属性为只读 IReadOnlyList 视图，EF Core 须经 backing field 读写
        builder.Property(a => a.Rules)
            .HasColumnName("rules")
            .HasConversion(
                v => JsonSerializer.Serialize(v, RuleJsonOptions),
                v => JsonSerializer.Deserialize<List<PromotionRule>>(v, RuleJsonOptions)
                     ?? new List<PromotionRule>())
            .HasField("_rules")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(a => a.Status).HasDatabaseName("ix_promotion_activities_status");
    }
}
