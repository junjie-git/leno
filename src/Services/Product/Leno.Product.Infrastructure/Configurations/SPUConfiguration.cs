using System.Text.Json;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Product.Infrastructure.Configurations;

/// <summary>
/// SPU 聚合根的 EF Core 映射配置（snake_case）。
/// SKU 经 HasMany 一对多映射（独立表 skus，FK spu_id，级联删除）；
/// 图片画廊经 OwnsMany 拆表 spu_images；规格维度 Specs、审核历史、价格变更历史、库存操作历史序列化为 JSON 列。
/// </summary>
public sealed class SPUConfiguration : IEntityTypeConfiguration<SPU>
{
    public void Configure(EntityTypeBuilder<SPU> builder)
    {
        builder.ToTable("spus");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ShopId).HasColumnName("shop_id");
        builder.Property(s => s.SellerId).HasColumnName("seller_id");
        builder.Property(s => s.Title).HasColumnName("title").HasMaxLength(100).IsRequired();
        builder.Property(s => s.Subtitle).HasColumnName("subtitle").HasMaxLength(200);
        builder.Property(s => s.MainImageUrl).HasColumnName("main_image_url").HasMaxLength(512).IsRequired();
        builder.Property(s => s.CategoryId).HasColumnName("category_id");
        builder.Property(s => s.BrandId).HasColumnName("brand_id");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(s => s.SuspendedByShop).HasColumnName("suspended_by_shop");
        builder.Property(s => s.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(s => s.Score).HasColumnName("average_score");
        builder.Property(s => s.ReviewCount).HasColumnName("review_count");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.Property(s => s.Version).HasColumnName("version").IsRowVersion();

        // 规格维度名集合序列化为 JSON 列
        builder.Property(s => s.Specs)
            .HasColumnName("specs")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        // 审核历史序列化为 JSON 列
        builder.Property<List<AuditInfo>>("_auditHistory")
            .HasColumnName("audit_history")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<AuditInfo>>(v, (JsonSerializerOptions?)null) ?? new List<AuditInfo>());

        // 价格变更历史序列化为 JSON 列
        builder.Property<List<PriceChangeRecord>>("_priceChangeHistory")
            .HasColumnName("price_change_history")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<PriceChangeRecord>>(v, (JsonSerializerOptions?)null) ?? new List<PriceChangeRecord>());

        // 库存操作历史序列化为 JSON 列
        builder.Property<List<StockOperationRecord>>("_stockOperationHistory")
            .HasColumnName("stock_operation_history")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<StockOperationRecord>>(v, (JsonSerializerOptions?)null) ?? new List<StockOperationRecord>());

        // 图片画廊作为拥有值对象集合拆表
        builder.OwnsMany(s => s.Images, img =>
        {
            img.ToTable("spu_images");
            img.Property(i => i.Url).HasColumnName("url").HasMaxLength(512).IsRequired();
            img.Property(i => i.SortOrder).HasColumnName("sort_order");
            img.Property(i => i.IsMain).HasColumnName("is_main");
        });

        // SKU 一对多，独立表，级联删除
        builder.HasMany(s => s.SKUs)
            .WithOne()
            .HasForeignKey(sku => sku.SpuId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.ShopId).HasDatabaseName("ix_spus_shop_id");
        builder.HasIndex(s => s.Status).HasDatabaseName("ix_spus_status");
        builder.HasIndex(s => s.CategoryId).HasDatabaseName("ix_spus_category_id");
    }
}
