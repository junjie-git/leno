using Leno.Product.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Product.Infrastructure.Configurations;

/// <summary>
/// PriceHistory 聚合的 EF Core 映射配置（snake_case）。
/// 独立表 price_histories，按 (spu_id, changed_at) 建联合索引用于按 SPU 维度查询变更轨迹。
/// </summary>
public sealed class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
{
    public void Configure(EntityTypeBuilder<PriceHistory> builder)
    {
        builder.ToTable("price_histories");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.SpuId).HasColumnName("spu_id");
        builder.Property(p => p.SkuId).HasColumnName("sku_id");
        builder.Property(p => p.OldPrice).HasColumnName("old_price").HasPrecision(18, 2);
        builder.Property(p => p.NewPrice).HasColumnName("new_price").HasPrecision(18, 2);
        builder.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(p => p.Reason).HasColumnName("reason").HasMaxLength(200);
        builder.Property(p => p.ChangedAt).HasColumnName("changed_at");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(p => new { p.SpuId, p.ChangedAt }).HasDatabaseName("ix_price_histories_spu_changed_at");
        builder.HasIndex(p => p.SkuId).HasDatabaseName("ix_price_histories_sku_id");
    }
}
