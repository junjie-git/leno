using Leno.SellerShop.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SellerShop.Infrastructure.Configurations;

/// <summary>
/// ShopDashboardData 读取模型的 EF Core 映射配置。
/// 与 Shop 一对一关联，ShopId 唯一索引。
/// </summary>
public sealed class ShopDashboardDataConfiguration : IEntityTypeConfiguration<ShopDashboardData>
{
    public void Configure(EntityTypeBuilder<ShopDashboardData> builder)
    {
        builder.ToTable("shop_dashboard_data");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.ShopId).HasColumnName("shop_id");
        builder.Property(d => d.TotalOrders).HasColumnName("total_orders");
        builder.Property(d => d.PendingOrders).HasColumnName("pending_orders");
        builder.Property(d => d.ConfirmedOrders).HasColumnName("confirmed_orders");
        builder.Property(d => d.CompletedOrders).HasColumnName("completed_orders");
        builder.Property(d => d.CancelledOrders).HasColumnName("cancelled_orders");
        builder.Property(d => d.TotalRevenue).HasColumnName("total_revenue").HasPrecision(18, 2);
        builder.Property(d => d.RefundedAmount).HasColumnName("refunded_amount").HasPrecision(18, 2);
        builder.Property(d => d.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(d => d.LastUpdatedAt).HasColumnName("last_updated_at");

        // 审计字段（基类 AggregateRoot 继承自 Entity，包含 CreatedAt/UpdatedAt/CreatedBy/UpdatedBy）。
        // 显式 snake_case 列映射 + CreatedBy/UpdatedBy 长度收窄至 64，避免默认 nvarchar(max) 与命名不一致。
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(d => d.ShopId).HasDatabaseName("ix_shop_dashboard_data_shop_id").IsUnique();
    }
}