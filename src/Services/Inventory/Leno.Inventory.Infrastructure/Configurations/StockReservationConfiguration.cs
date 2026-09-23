using Leno.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Inventory.Infrastructure.Configurations;

/// <summary>
/// StockReservation 库存台账条目的 EF Core 映射配置（snake_case）。
/// <para>
/// 唯一约束 (order_id, sku_id)：同一订单同一 SKU 只有一条占用记录 ——
/// 并发/重复预占在数据库层面被拒绝，配合单向状态机构成命令幂等的硬保证。
/// 状态查询索引 (order_id, status)：确认/释放/归还按订单解析待操作条目。
/// </para>
/// </summary>
public sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.OrderId).HasColumnName("order_id");
        builder.Property(s => s.SkuId).HasColumnName("sku_id");
        builder.Property(s => s.Quantity).HasColumnName("quantity");
        builder.Property(s => s.Status).HasColumnName("status");
        builder.Property(s => s.IdempotencyKey).HasColumnName("idempotency_key");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(s => new { s.OrderId, s.SkuId })
            .IsUnique()
            .HasDatabaseName("ux_stock_reservations_order_sku");
        builder.HasIndex(s => new { s.OrderId, s.Status })
            .HasDatabaseName("ix_stock_reservations_order_status");
        builder.HasIndex(s => s.SkuId)
            .HasDatabaseName("ix_stock_reservations_sku_id");
    }
}
