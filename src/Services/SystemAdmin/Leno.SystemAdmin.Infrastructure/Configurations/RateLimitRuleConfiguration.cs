using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// RateLimitRule 限流规则的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class RateLimitRuleConfiguration : IEntityTypeConfiguration<RateLimitRule>
{
    public void Configure(EntityTypeBuilder<RateLimitRule> builder)
    {
        builder.ToTable("rate_limit_rules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TargetApi).HasColumnName("target_api").HasMaxLength(256).IsRequired();
        builder.Property(r => r.TargetContext).HasColumnName("target_context").HasMaxLength(64);
        builder.Property(r => r.Limit).HasColumnName("limit").IsRequired();
        builder.Property(r => r.WindowSeconds).HasColumnName("window_seconds").IsRequired();
        builder.Property(r => r.Algorithm).HasColumnName("algorithm").IsRequired();
        builder.Property(r => r.Scope).HasColumnName("scope").IsRequired();
        builder.Property(r => r.Enabled).HasColumnName("enabled").IsRequired();

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(r => r.TargetApi).HasDatabaseName("ix_rate_limit_rules_target_api");
        builder.HasIndex(r => r.Enabled).HasDatabaseName("ix_rate_limit_rules_enabled");

        // 乐观并发控制：IsRowVersion() 让 EF Core 在 Update 时将 Version 加入 WHERE 子句，
        // 不匹配则抛 DbUpdateConcurrencyException，由控制器捕获并返回 409 Conflict。
        builder.Property(r => r.Version).HasColumnName("version").IsRowVersion();
    }
}