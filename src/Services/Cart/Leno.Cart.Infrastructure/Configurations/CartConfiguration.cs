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
        builder.Property(c => c.Version).HasColumnName("version").IsRowVersion();

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
        builder.Property(i => i.Version).HasColumnName("version").IsRowVersion();

        builder.HasIndex(i => i.SkuId).HasDatabaseName("ix_cart_items_sku_id");
        builder.HasIndex(i => i.SellerId).HasDatabaseName("ix_cart_items_seller_id");
    }
}
