using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Promotion.Infrastructure.Configurations;

/// <summary>
/// Coupon 优惠券模板聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupons");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(c => c.Type).HasColumnName("type").HasConversion<int>();
        builder.Property(c => c.FaceValue).HasColumnName("face_value").HasPrecision(18, 2);
        builder.Property(c => c.MinSpend).HasColumnName("min_spend").HasPrecision(18, 2);
        builder.Property(c => c.ValidityType).HasColumnName("validity_type").HasConversion<int>();
        builder.Property(c => c.ValidFrom).HasColumnName("valid_from");
        builder.Property(c => c.ValidTo).HasColumnName("valid_to");
        builder.Property(c => c.ValidDays).HasColumnName("valid_days");
        builder.Property(c => c.TotalQty).HasColumnName("total_qty");
        builder.Property(c => c.IssuedQty).HasColumnName("issued_qty");
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(c => c.Status).HasDatabaseName("ix_coupons_status");
    }
}
