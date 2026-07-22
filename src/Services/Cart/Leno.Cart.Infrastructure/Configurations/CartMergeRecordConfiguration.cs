using Leno.Cart.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Cart.Infrastructure.Configurations;

/// <summary>
/// CartMergeRecord 实体的 EF Core 映射配置（snake_case）。
/// 表 cart_merge_records，主键 anonymous_id，无乐观锁版本字段（非聚合根，简单记录表）。
/// </summary>
public sealed class CartMergeRecordConfiguration : IEntityTypeConfiguration<CartMergeRecord>
{
    public void Configure(EntityTypeBuilder<CartMergeRecord> builder)
    {
        builder.ToTable("cart_merge_records");
        builder.HasKey(r => r.AnonymousId);

        builder.Property(r => r.AnonymousId).HasColumnName("anonymous_id").HasMaxLength(128);
        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.MergedAt).HasColumnName("merged_at");
        builder.Property(r => r.MergedCount).HasColumnName("merged_count");
    }
}
