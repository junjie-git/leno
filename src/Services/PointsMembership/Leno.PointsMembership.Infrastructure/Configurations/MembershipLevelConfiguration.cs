using Leno.PointsMembership.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.PointsMembership.Infrastructure.Configurations;

/// <summary>
/// MembershipLevel 会员等级聚合根的 EF Core 映射配置（snake_case）。
/// 按等级编号建立唯一索引，等级编号全局唯一。
/// </summary>
public sealed class MembershipLevelConfiguration : IEntityTypeConfiguration<MembershipLevel>
{
    public void Configure(EntityTypeBuilder<MembershipLevel> builder)
    {
        builder.ToTable("membership_levels");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
        builder.Property(l => l.Level).HasColumnName("level");
        builder.Property(l => l.MinConsumption).HasColumnName("min_consumption").HasPrecision(18, 2);
        builder.Property(l => l.DiscountRate).HasColumnName("discount_rate").HasPrecision(3, 2);
        builder.Property(l => l.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(l => l.Level).IsUnique().HasDatabaseName("ix_membership_levels_level");
    }
}
