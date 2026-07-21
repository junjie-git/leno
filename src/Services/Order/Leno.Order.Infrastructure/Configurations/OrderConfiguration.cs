using Leno.Order.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure.Configurations;

/// <summary>
/// Order 订单聚合根的 EF Core 映射配置（snake_case）。
/// Items 明细集合与 AddressSnapshot 地址快照作为 owned type 持久化。
/// </summary>
public sealed class OrderConfiguration : IEntityTypeConfiguration<OrderAggregate>
{
    public void Configure(EntityTypeBuilder<OrderAggregate> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.OrderNo).HasColumnName("order_no").HasMaxLength(64).IsRequired();
        builder.Property(o => o.OrderType).HasColumnName("order_type").HasConversion<int>();
        builder.Property(o => o.UserId).HasColumnName("user_id");
        builder.Property(o => o.SellerId).HasColumnName("seller_id");
        builder.Property(o => o.ItemsAmount).HasColumnName("items_amount");
        builder.Property(o => o.DiscountAmount).HasColumnName("discount_amount");
        builder.Property(o => o.PointsOffsetAmount).HasColumnName("points_offset_amount");
        builder.Property(o => o.FreightAmount).HasColumnName("freight_amount");
        builder.Property(o => o.TotalAmount).HasColumnName("total_amount");
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(o => o.PaymentMethod).HasColumnName("payment_method").HasConversion<int>();
        builder.Property(o => o.PaymentInitiated).HasColumnName("payment_initiated");
        builder.Property(o => o.PaymentInitiatedAt).HasColumnName("payment_initiated_at");
        builder.Property(o => o.ExpireAt).HasColumnName("expire_at");
        builder.Property(o => o.PaidAt).HasColumnName("paid_at");
        builder.Property(o => o.PaymentId).HasColumnName("payment_id");
        builder.Property(o => o.TradeNo).HasColumnName("trade_no").HasMaxLength(128);
        builder.Property(o => o.ShippedAt).HasColumnName("shipped_at");
        builder.Property(o => o.LogisticsNo).HasColumnName("logistics_no").HasMaxLength(128);
        builder.Property(o => o.CompletedAt).HasColumnName("completed_at");
        builder.Property(o => o.AfterSalesWindowEndsAt).HasColumnName("after_sales_window_ends_at");
        builder.Property(o => o.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(o => o.CancelReason).HasColumnName("cancel_reason").HasMaxLength(512);

        // 乐观并发控制：RowVersion 由数据库自动生成与校验，并发写入时抛 DbUpdateConcurrencyException
        builder.Property(o => o.RowVersion).HasColumnName("row_version").IsRowVersion();

        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 收货地址快照，作为 owned type 映射到 orders 表
        builder.OwnsOne(o => o.AddressSnapshot, address =>
        {
            address.Property(a => a.RecipientName).HasColumnName("recipient_name").HasMaxLength(64).IsRequired();
            address.Property(a => a.RecipientPhone).HasColumnName("recipient_phone").HasMaxLength(32).IsRequired();
            address.Property(a => a.Province).HasColumnName("province").HasMaxLength(64).IsRequired();
            address.Property(a => a.City).HasColumnName("city").HasMaxLength(64).IsRequired();
            address.Property(a => a.District).HasColumnName("district").HasMaxLength(64).IsRequired();
            address.Property(a => a.Detail).HasColumnName("address_detail").HasMaxLength(256).IsRequired();
        });

        // 订单明细集合，作为 owned collection 映射到 order_items 表
        builder.OwnsMany(o => o.Items, item =>
        {
            item.ToTable("order_items");
            item.HasKey(i => i.Id);

            item.Property(i => i.Id).HasColumnName("id");
            item.Property(i => i.SkuId).HasColumnName("sku_id");
            item.Property(i => i.UnitPrice).HasColumnName("unit_price");
            item.Property(i => i.Quantity).HasColumnName("quantity");
            item.Property(i => i.DiscountAllocation).HasColumnName("discount_allocation");
            item.Property(i => i.Subtotal).HasColumnName("subtotal");
            item.Property(i => i.SourceCartItemId).HasColumnName("source_cart_item_id");
            item.Property(i => i.CreatedAt).HasColumnName("created_at");
            item.Property(i => i.UpdatedAt).HasColumnName("updated_at");
            item.Property(i => i.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
            item.Property(i => i.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

            // 商品快照，作为 owned type 映射到 order_items 表
            item.OwnsOne(i => i.ProductSnapshot, snapshot =>
            {
                snapshot.Property(p => p.SkuId).HasColumnName("product_sku_id");
                snapshot.Property(p => p.SpuId).HasColumnName("product_spu_id");
                snapshot.Property(p => p.ProductName).HasColumnName("product_name").HasMaxLength(256).IsRequired();
                snapshot.Property(p => p.SkuName).HasColumnName("product_sku_name").HasMaxLength(256).IsRequired();
                snapshot.Property(p => p.MainImage).HasColumnName("product_main_image").HasMaxLength(512);
                snapshot.Property(p => p.SellerId).HasColumnName("product_seller_id");
            });
        });

        builder.HasIndex(o => o.OrderNo).IsUnique().HasDatabaseName("ix_orders_order_no");
        builder.HasIndex(o => o.UserId).HasDatabaseName("ix_orders_user_id");
        builder.HasIndex(o => o.SellerId).HasDatabaseName("ix_orders_seller_id");
        builder.HasIndex(o => o.Status).HasDatabaseName("ix_orders_status");
    }
}
