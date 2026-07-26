using Leno.PointsMembership.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.PointsMembership.Infrastructure.Configurations;

/// <summary>
/// PointsRule 积分规则聚合根的 EF Core 映射配置（snake_case）。
/// 按规则编码建立唯一索引，编码全局唯一。
/// </summary>
public sealed class PointsRuleConfiguration : IEntityTypeConfiguration<PointsRule>
{
    public void Configure(EntityTypeBuilder<PointsRule> builder)
    {
        builder.ToTable("points_rules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(r => r.ActionType).HasColumnName("action_type").HasConversion<int>();
        builder.Property(r => r.Points).HasColumnName("points");
        builder.Property(r => r.DailyLimit).HasColumnName("daily_limit");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(r => r.Code).IsUnique().HasDatabaseName("ix_points_rules_code");
        builder.HasIndex(r => r.Status).HasDatabaseName("ix_points_rules_status");
    }
}
