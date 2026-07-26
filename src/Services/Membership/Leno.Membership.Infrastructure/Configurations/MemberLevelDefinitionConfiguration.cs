using Leno.Membership.Domain.Aggregates.MemberLevelDefinition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MemberLevelDefinitionAggregate = Leno.Membership.Domain.Aggregates.MemberLevelDefinition.MemberLevelDefinition;

namespace Leno.Membership.Infrastructure.Configurations;

/// <summary>
/// MemberLevelDefinition 会员等级定义聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class MemberLevelDefinitionConfiguration : IEntityTypeConfiguration<MemberLevelDefinitionAggregate>
{
    public void Configure(EntityTypeBuilder<MemberLevelDefinitionAggregate> builder)
    {
        builder.ToTable("member_level_definitions");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.Level).HasColumnName("level");
        builder.Property(l => l.Name).HasColumnName("name").HasMaxLength(64);
        builder.Property(l => l.MinGrowthValue).HasColumnName("min_growth_value");
        builder.Property(l => l.MaxGrowthValue).HasColumnName("max_growth_value");
        builder.Property(l => l.Description).HasColumnName("description").HasMaxLength(512);
        builder.Property(l => l.LevelUpBonusPoints).HasColumnName("level_up_bonus_points");
        builder.Property(l => l.Status).HasColumnName("status");

        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(l => l.Level).IsUnique().HasDatabaseName("ix_member_level_definitions_level");
    }
}
