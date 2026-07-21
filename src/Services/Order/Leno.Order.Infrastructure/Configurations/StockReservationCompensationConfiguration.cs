using Leno.Order.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Order.Infrastructure.Configurations;

/// <summary>
/// StockReservationCompensation 库存预占回滚补偿聚合根的 EF Core 映射配置（snake_case）（T18）。
/// </summary>
public sealed class StockReservationCompensationConfiguration : IEntityTypeConfiguration<StockReservationCompensation>
{
    public void Configure(EntityTypeBuilder<StockReservationCompensation> builder)
    {
        builder.ToTable("stock_reservation_compensations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.OrderId).HasColumnName("order_id");
        builder.Property(c => c.SkuId).HasColumnName("sku_id");
        builder.Property(c => c.Quantity).HasColumnName("quantity");
        builder.Property(c => c.Status).HasColumnName("status");
        // RetryCount 为只读属性（底层 _retryCount 字段由 Interlocked.Increment 原子自增），
        // 显式声明 backing field 与 Field 访问模式，确保 EF Core 经字段读写（P1-T20）
        builder.Property(c => c.RetryCount)
            .HasColumnName("retry_count")
            .HasField("_retryCount")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(c => c.MaxRetries).HasColumnName("max_retries");
        builder.Property(c => c.LastAttemptedAt).HasColumnName("last_attempted_at");
        builder.Property(c => c.LastErrorMessage).HasColumnName("last_error_message").HasMaxLength(500);

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 索引：按状态查询 Pending 记录 + 按订单查询
        builder.HasIndex(c => c.Status).HasDatabaseName("ix_stock_compensations_status");
        builder.HasIndex(c => c.OrderId).HasDatabaseName("ix_stock_compensations_order_id");
    }
}
