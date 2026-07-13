using Leno.Order.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Order.Infrastructure.Configurations;

/// <summary>
/// StockReservation 库存预占聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SkuId).HasColumnName("sku_id");
        builder.Property(s => s.BaseLineQty).HasColumnName("base_line_qty");
        builder.Property(s => s.ReservedQty).HasColumnName("reserved_qty");
        builder.Property(s => s.DeductedQty).HasColumnName("deducted_qty");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(s => s.SkuId).IsUnique().HasDatabaseName("ix_stock_reservations_sku_id");
    }
}
