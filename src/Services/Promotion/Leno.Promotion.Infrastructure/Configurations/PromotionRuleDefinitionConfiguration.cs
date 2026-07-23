using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Promotion.Infrastructure.Configurations;

/// <summary>
/// PromotionRuleDefinition 聚合根的 EF Core 映射配置（snake_case）。
/// 规则体 <see cref="PromotionRuleDefinition.DefinitionJson"/> 直接以 nvarchar(max) 存储 JSON 文本，
/// 由 <c>JsonRuleLoader</c> 反序列化为 <c>JsonRuleDefinition</c>。
/// </summary>
public sealed class PromotionRuleDefinitionConfiguration : IEntityTypeConfiguration<PromotionRuleDefinition>
{
    public void Configure(EntityTypeBuilder<PromotionRuleDefinition> builder)
    {
        builder.ToTable("promotion_rule_definitions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.RuleType).HasColumnName("rule_type").HasMaxLength(64).IsRequired();
        builder.Property(r => r.DisplayName).HasColumnName("display_name").HasMaxLength(128).IsRequired();
        builder.Property(r => r.Priority).HasColumnName("priority");
        builder.Property(r => r.Stacking).HasColumnName("stacking").HasConversion<int>();
        builder.Property(r => r.DefinitionJson).HasColumnName("definition_json").HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(r => r.Enabled).HasColumnName("enabled");
        // 业务版本号列名 definition_version，避免与 BaseDbContext 自动注入的 rowversion shadow property "version" 名冲突
        builder.Property(r => r.DefinitionVersion).HasColumnName("definition_version").HasMaxLength(32).IsRequired();
        builder.Property(r => r.Remark).HasColumnName("remark").HasMaxLength(512);

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 同 RuleType 启用规则唯一索引，确保一种规则类型至多一条启用定义，避免 JsonRuleLoader 歧义
        builder.HasIndex(r => new { r.RuleType, r.Enabled })
            .IsUnique()
            .HasFilter("[enabled] = 1")
            .HasDatabaseName("ux_promotion_rule_definitions_rule_type_enabled");

        builder.HasIndex(r => r.Priority).HasDatabaseName("ix_promotion_rule_definitions_priority");
    }
}
