using Leno.PointsMembership.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.PointsMembership.Infrastructure.Configurations;

/// <summary>
/// PointsAccount 积分账户聚合根的 EF Core 映射配置（snake_case）。
/// FrozenEntries 冻结明细集合作为聚合子实体经一对多关系持久化。
/// </summary>
public sealed class PointsAccountConfiguration : IEntityTypeConfiguration<PointsAccount>
{
    public void Configure(EntityTypeBuilder<PointsAccount> builder)
    {
        builder.ToTable("points_accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.Balance).HasColumnName("balance");
        builder.Property(a => a.FrozenBalance).HasColumnName("frozen_balance");
        builder.Property(a => a.TotalEarned).HasColumnName("total_earned");
        builder.Property(a => a.TotalSpent).HasColumnName("total_spent");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.Property(a => a.Version).HasColumnName("version").IsRowVersion();

        // FrozenEntries 冻结明细集合，外键为影子属性 points_account_id，删除账户级联删除明细。
        builder.HasMany(a => a.FrozenEntries)
            .WithOne()
            .HasForeignKey("PointsAccountId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.UserId).IsUnique().HasDatabaseName("ix_points_accounts_user_id");
    }
}
