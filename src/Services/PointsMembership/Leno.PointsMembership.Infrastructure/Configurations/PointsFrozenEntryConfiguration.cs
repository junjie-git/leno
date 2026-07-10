using Leno.PointsMembership.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.PointsMembership.Infrastructure.Configurations;

/// <summary>
/// PointsFrozenEntry 积分冻结明细子实体的 EF Core 映射配置（snake_case）。
/// 隶属积分账户聚合，外键为影子属性，按订单标识跟踪冻结积分。
/// </summary>
public sealed class PointsFrozenEntryConfiguration : IEntityTypeConfiguration<PointsFrozenEntry>
{
    public void Configure(EntityTypeBuilder<PointsFrozenEntry> builder)
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
        builder.Property(e => e.Version).HasColumnName("version").IsRowVersion();

        // 反向配置与 PointsAccount 的一对多关系，并显式设置影子外键列名。
        builder.HasOne<PointsAccount>()
            .WithMany(a => a.FrozenEntries)
            .HasForeignKey("PointsAccountId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property("PointsAccountId").HasColumnName("points_account_id");

        builder.HasIndex(e => e.OrderId).HasDatabaseName("ix_points_frozen_entries_order_id");
    }
}
