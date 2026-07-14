using Leno.Product.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Product.Infrastructure.Configurations;

/// <summary>
/// StockBaseline 聚合根的 EF Core 映射配置（snake_case）。
/// SkuId 唯一索引保证一 SKU 一基线；状态以整型落库。
/// </summary>
public sealed class StockBaselineConfiguration : IEntityTypeConfiguration<StockBaseline>
{
    public void Configure(EntityTypeBuilder<StockBaseline> builder)
    {
        builder.ToTable("stock_baselines");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SkuId).HasColumnName("sku_id");
        builder.Property(s => s.AvailableQty).HasColumnName("available_qty");
        builder.Property(s => s.ReservedQty).HasColumnName("reserved_qty");
        builder.Property(s => s.DeductedQty).HasColumnName("deducted_qty");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.HasIndex(s => s.SkuId).HasDatabaseName("ix_stock_baselines_sku_id").IsUnique();
    }
}
