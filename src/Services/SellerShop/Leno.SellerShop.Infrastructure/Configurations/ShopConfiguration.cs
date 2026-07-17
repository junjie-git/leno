using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SellerShop.Infrastructure.Configurations;

/// <summary>
/// Shop 聚合根的 EF Core 映射配置。
/// 表名 snake_case；SellerId 唯一索引（一卖家一店铺）；状态以整型落库。
/// </summary>
public sealed class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("shops");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SellerId).HasColumnName("seller_id");
        builder.Property(s => s.ShopName).HasColumnName("shop_name").HasMaxLength(32).IsRequired();
        builder.Property(s => s.Logo).HasColumnName("logo").HasMaxLength(512);
        builder.Property(s => s.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(s => s.ContactPhone).HasColumnName("contact_phone").HasMaxLength(20).IsRequired();
        builder.Property(s => s.ContactEmail).HasColumnName("contact_email").HasMaxLength(256);
        builder.Property(s => s.BusinessLicenseNo).HasColumnName("business_license_no").HasMaxLength(32);
        builder.Property(s => s.Address).HasColumnName("address").HasMaxLength(256);
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(s => s.ProductCount).HasColumnName("product_count");
        builder.Property(s => s.StatusReason).HasColumnName("status_reason").HasMaxLength(200);
        builder.Property(s => s.ReviewedBy).HasColumnName("reviewed_by");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.HasIndex(s => s.SellerId).HasDatabaseName("ix_shops_seller_id").IsUnique();
        builder.HasIndex(s => s.Status).HasDatabaseName("ix_shops_status");

        builder.HasMany<ShopQualification>("Qualifications")
            .WithOne()
            .HasForeignKey("ShopId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
