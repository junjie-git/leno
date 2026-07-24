using Leno.Points.Domain.Aggregates.PointsAccount;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Points.Infrastructure.Configurations;

/// <summary>
/// PointsAccount 积分账户聚合根的 EF Core 映射配置（snake_case）。
/// FrozenEntries 冻结明细集合作为聚合子实体经一对多关系持久化。
/// PointsBalance 值对象作为 owned entity 持久化到 points_accounts 表的子列。
/// </summary>
public sealed class PointsAccountConfiguration : IEntityTypeConfiguration<PointsAccount>
{
    public void Configure(EntityTypeBuilder<PointsAccount> builder)
    {
        builder.ToTable("points_accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.UserId).HasColumnName("user_id");

        // PointsBalance 值对象作为 owned entity 持久化（拆列为多个列）
        builder.OwnsOne(a => a.Balance, b =>
        {
            b.Property(p => p.Available).HasColumnName("balance");
            b.Property(p => p.Frozen).HasColumnName("frozen_balance");
            b.Property(p => p.TotalEarned).HasColumnName("total_earned");
            b.Property(p => p.TotalSpent).HasColumnName("total_spent");
        });

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // FrozenEntries 冻结明细集合，外键为影子属性 points_account_id，删除账户级联删除明细。
        builder.HasMany(a => a.FrozenEntries)
            .WithOne()
            .HasForeignKey("PointsAccountId")
            .OnDelete(DeleteBehavior.Cascade);

        // Flows 积分流水集合，外键为影子属性 points_account_id，删除账户级联删除流水。
        builder.HasMany(a => a.Flows)
            .WithOne()
            .HasForeignKey("PointsAccountId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.UserId).IsUnique().HasDatabaseName("ix_points_accounts_user_id");
    }
}

/// <summary>
/// FrozenPoints 积分冻结明细子实体的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class FrozenPointsConfiguration : IEntityTypeConfiguration<FrozenPoints>
{
    public void Configure(EntityTypeBuilder<FrozenPoints> builder)
    {
        builder.ToTable("points_frozen_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.OrderId).HasColumnName("order_id");
        builder.Property(e => e.Amount).HasColumnName("amount");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(e => e.OrderId).HasDatabaseName("ix_points_frozen_entries_order_id");
    }
}
