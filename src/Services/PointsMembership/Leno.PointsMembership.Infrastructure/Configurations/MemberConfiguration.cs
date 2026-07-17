using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.PointsMembership.Infrastructure.Configurations;

/// <summary>
/// Member 会员聚合根的 EF Core 映射配置（snake_case）。
/// 按用户标识建立唯一索引，一个用户对应一个会员聚合。
/// </summary>
public sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("members");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.UserId).HasColumnName("user_id");
        builder.Property(m => m.CurrentLevel).HasColumnName("current_level");
        builder.Property(m => m.TotalConsumption).HasColumnName("total_consumption").HasPrecision(18, 2);
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at");
        builder.Property(m => m.LevelUpgradedAt).HasColumnName("level_upgraded_at");
        builder.Property(m => m.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(m => m.UserId).IsUnique().HasDatabaseName("ix_members_user_id");

        // 等级变更历史作为 owned collection 持久化到 member_level_change_histories 表
        // 由 EF Core 自动生成 shadow FK (member_id) + shadow index 作为复合主键
        builder.OwnsMany(m => m.LevelChangeHistories, h =>
        {
            h.ToTable("member_level_change_histories");
            h.Property(x => x.OldLevel).HasColumnName("old_level");
            h.Property(x => x.NewLevel).HasColumnName("new_level");
            h.Property(x => x.GrowthValue).HasColumnName("growth_value");
            h.Property(x => x.ChangedAt).HasColumnName("changed_at");
            h.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(512);
        });
    }
}
