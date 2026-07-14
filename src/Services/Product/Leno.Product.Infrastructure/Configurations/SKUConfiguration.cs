using System.Text.Json;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Product.Infrastructure.Configurations;

/// <summary>
/// SKU 实体的 EF Core 映射配置（snake_case）。
/// Price 经 OwnsOne 拆金额与币种两列；SpecAttributes（SkuSpec）序列化为 JSON 列。
/// </summary>
public sealed class SKUConfiguration : IEntityTypeConfiguration<SKU>
{
    public void Configure(EntityTypeBuilder<SKU> builder)
    {
        builder.ToTable("skus");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SpuId).HasColumnName("spu_id");
        builder.Property(s => s.SkuCode).HasColumnName("sku_code").HasMaxLength(64).IsRequired();
        builder.Property(s => s.StockQty).HasColumnName("stock_qty");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(s => s.ImageUrl).HasColumnName("image_url").HasMaxLength(512);

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        // Money 值对象拆列
        builder.OwnsOne(s => s.Price, money =>
        {
            money.Property(p => p.Amount).HasColumnName("price").HasPrecision(18, 2).IsRequired();
            money.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        // SkuSpec 规格集合序列化为 JSON 列
        builder.Property(s => s.SpecAttributes)
            .HasColumnName("spec_attributes")
            .HasConversion(
                v => JsonSerializer.Serialize(v.Attributes, (JsonSerializerOptions?)null),
                v => SkuSpec.Create(JsonSerializer.Deserialize<List<SpecAttribute>>(v, (JsonSerializerOptions?)null)
                    ?? new List<SpecAttribute>()));

        builder.HasIndex(s => s.SpuId).HasDatabaseName("ix_skus_spu_id");
        builder.HasIndex(s => s.SkuCode).HasDatabaseName("ix_skus_sku_code");
    }
}
