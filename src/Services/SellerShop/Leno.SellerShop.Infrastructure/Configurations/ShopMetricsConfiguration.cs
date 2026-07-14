using Leno.SellerShop.Domain.Aggregates;
using Leno.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SellerShop.Infrastructure.Configurations;

/// <summary>
/// ShopMetrics 聚合根的 EF Core 映射配置。
/// 表名 snake_case；SalesAmount 经 OwnsOne 拆为金额与币种两列；
/// (ShopId, Date) 复合唯一索引支撑 upsert。
/// </summary>
public sealed class ShopMetricsConfiguration : IEntityTypeConfiguration<ShopMetrics>
{
    public void Configure(EntityTypeBuilder<ShopMetrics> builder)
    {
        builder.ToTable("shop_metrics");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.ShopId).HasColumnName("shop_id");
        builder.Property(m => m.Date).HasColumnName("date");
        builder.Property(m => m.OrderCount).HasColumnName("order_count");
        builder.Property(m => m.ProductCount).HasColumnName("product_count");
        builder.Property(m => m.AvgRating).HasColumnName("avg_rating").HasPrecision(5, 2);
        builder.Property(m => m.RatingSum).HasColumnName("rating_sum").HasPrecision(10, 2);
        builder.Property(m => m.RatingCount).HasColumnName("rating_count");
        builder.Property(m => m.RefundCount).HasColumnName("refund_count");

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        // Money 值对象拆列：金额与币种同表落库，作为聚合拥有的值对象。
        builder.OwnsOne(m => m.SalesAmount, money =>
        {
            money.Property(p => p.Amount).HasColumnName("sales_amount").HasPrecision(18, 2);
            money.Property(p => p.Currency).HasColumnName("sales_currency").HasMaxLength(3).IsRequired();
        });

        builder.OwnsOne(m => m.RefundAmount, money =>
        {
            money.Property(p => p.Amount).HasColumnName("refund_amount").HasPrecision(18, 2);
            money.Property(p => p.Currency).HasColumnName("refund_currency").HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(m => new { m.ShopId, m.Date })
            .HasDatabaseName("ix_shop_metrics_shop_date")
            .IsUnique();
        builder.HasIndex(m => m.ShopId).HasDatabaseName("ix_shop_metrics_shop_id");
    }
}
