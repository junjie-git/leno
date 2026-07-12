using Leno.SellerShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SellerShop.Infrastructure.Configurations;

/// <summary>
/// ShopQualification 实体的 EF Core 映射配置（snake_case）。
/// 资质属于 Shop 聚合内部实体，通过 ShopId 外键关联。
/// </summary>
public sealed class ShopQualificationConfiguration : IEntityTypeConfiguration<ShopQualification>
{
    public void Configure(EntityTypeBuilder<ShopQualification> builder)
    {
        builder.ToTable("shop_qualifications");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id).HasColumnName("id");
        builder.Property(q => q.ShopId).HasColumnName("shop_id").IsRequired();
        builder.Property(q => q.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        builder.Property(q => q.Number).HasColumnName("number").HasMaxLength(64).IsRequired();
        builder.Property(q => q.ImageUrl).HasColumnName("image_url").HasMaxLength(512).IsRequired();
        builder.Property(q => q.ValidFrom).HasColumnName("valid_from").IsRequired();
        builder.Property(q => q.ValidTo).HasColumnName("valid_to").IsRequired();
        builder.Property(q => q.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(q => q.RejectReason).HasColumnName("reject_reason").HasMaxLength(200);
        builder.Property(q => q.ReviewedBy).HasColumnName("reviewed_by");

        builder.Property(q => q.CreatedAt).HasColumnName("created_at");
        builder.Property(q => q.UpdatedAt).HasColumnName("updated_at");
        builder.Property(q => q.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(q => q.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(q => q.ShopId).HasDatabaseName("ix_shop_qualifications_shop_id");
        builder.HasIndex(q => q.Status).HasDatabaseName("ix_shop_qualifications_status");
        builder.HasIndex(q => new { q.ShopId, q.Type }).HasDatabaseName("ix_shop_qualifications_shop_type");
    }
}