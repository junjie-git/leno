using Leno.Cart.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Configurations;

/// <summary>
/// Cart 聚合根的 EF Core 映射配置（snake_case）。
/// CartItem 经 HasMany 一对多映射（独立表 cart_items，FK cart_id，级联删除）。
/// </summary>
public sealed class CartConfiguration : IEntityTypeConfiguration<CartAggregate>
{
    public void Configure(EntityTypeBuilder<CartAggregate> builder)
    {
        builder.ToTable("carts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.UserId).HasColumnName("user_id");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // P1-1：Revision 仅用于 Redis 匿名购物车 CAS 乐观并发，不持久化到 EF Core（认证购物车路径使用 rowversion shadow property）
        builder.Ignore(c => c.Revision);

        // CartItem 一对多，独立表，级联删除
        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.UserId).IsUnique().HasDatabaseName("ix_carts_user_id");
    }
}

/// <summary>
/// CartItem 实体的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.CartId).HasColumnName("cart_id");
        builder.Property(i => i.SkuId).HasColumnName("sku_id");
        builder.Property(i => i.SellerId).HasColumnName("seller_id");
        builder.Property(i => i.Quantity).HasColumnName("quantity");
        builder.Property(i => i.IsSelected).HasColumnName("is_selected");
        builder.Property(i => i.SourceCartItemId).HasColumnName("source_cart_item_id");

        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 阶段三 3.11：SkuSnapshot 作为 owned entity 映射到 cart_items 表的 sku_snapshot_* 列。
        // 所有列可空，允许历史购物车项渐进回填。Ownership 设为可选（SkuSnapshot 可为 null）。
        // EF Core 在读取时若所有 owned 列均为 NULL，则将 SkuSnapshot 设为 null。
        builder.OwnsOne(i => i.SkuSnapshot, snapshot =>
        {
            snapshot.Property(s => s.SkuId).HasColumnName("sku_snapshot_sku_id");
            snapshot.Property(s => s.SkuName).HasColumnName("sku_snapshot_sku_name").HasMaxLength(256);
            snapshot.Property(s => s.Price).HasColumnName("sku_snapshot_price").HasPrecision(18, 2);
            snapshot.Property(s => s.Currency).HasColumnName("sku_snapshot_currency").HasMaxLength(8);
            snapshot.Property(s => s.MainImageUrl).HasColumnName("sku_snapshot_main_image_url").HasMaxLength(1024);
            snapshot.Property(s => s.SpecText).HasColumnName("sku_snapshot_spec_text").HasMaxLength(512);
            snapshot.Property(s => s.Available).HasColumnName("sku_snapshot_available");
            snapshot.Property(s => s.SnapshotVersion).HasColumnName("sku_snapshot_version");
            snapshot.Property(s => s.SnapshotAt).HasColumnName("sku_snapshot_at");
        })
        .Navigation(i => i.SkuSnapshot)
        .IsRequired(false);

        builder.HasIndex(i => i.SkuId).HasDatabaseName("ix_cart_items_sku_id");
        builder.HasIndex(i => i.SellerId).HasDatabaseName("ix_cart_items_seller_id");
    }
}
