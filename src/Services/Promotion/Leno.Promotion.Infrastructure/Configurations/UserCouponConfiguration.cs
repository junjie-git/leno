using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Promotion.Infrastructure.Configurations;

/// <summary>
/// UserCoupon 用户优惠券聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class UserCouponConfiguration : IEntityTypeConfiguration<UserCoupon>
{
    public void Configure(EntityTypeBuilder<UserCoupon> builder)
    {
        builder.ToTable("user_coupons");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.UserId).HasColumnName("user_id");
        builder.Property(u => u.CouponId).HasColumnName("coupon_id");
        builder.Property(u => u.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(u => u.Source).HasColumnName("source").HasMaxLength(32);
        builder.Property(u => u.ReceivedAt).HasColumnName("received_at");
        builder.Property(u => u.UsedAt).HasColumnName("used_at");
        builder.Property(u => u.UsedOrderId).HasColumnName("used_order_id");
        builder.Property(u => u.LockedOrderId).HasColumnName("locked_order_id");
        builder.Property(u => u.ExpiredAt).HasColumnName("expired_at");

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.Property(u => u.Version).HasColumnName("version").IsRowVersion();

        builder.HasIndex(u => u.UserId).HasDatabaseName("ix_user_coupons_user_id");
        builder.HasIndex(u => u.CouponId).HasDatabaseName("ix_user_coupons_coupon_id");
        builder.HasIndex(u => u.LockedOrderId).HasDatabaseName("ix_user_coupons_locked_order_id");
    }
}
