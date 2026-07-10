using Leno.Order.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Order.Infrastructure.Configurations;

/// <summary>
/// FreightTemplate 运费模板聚合根的 EF Core 映射配置（snake_case）。
/// RegionRules 区域运费规则集合作为 owned collection 持久化。
/// </summary>
public sealed class FreightTemplateConfiguration : IEntityTypeConfiguration<FreightTemplate>
{
    public void Configure(EntityTypeBuilder<FreightTemplate> builder)
    {
        builder.ToTable("freight_templates");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(f => f.Type).HasColumnName("type").HasConversion<int>();
        builder.Property(f => f.FreeShippingThreshold).HasColumnName("free_shipping_threshold");
        builder.Property(f => f.SellerId).HasColumnName("seller_id");
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(f => f.Version).HasColumnName("version").IsRowVersion();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        builder.Property(f => f.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 区域运费规则集合，作为 owned collection 映射到 freight_region_rules 表
        builder.OwnsMany(f => f.RegionRules, rule =>
        {
            rule.ToTable("freight_region_rules");
            rule.HasKey(r => r.RegionCode);

            rule.Property(r => r.RegionCode).HasColumnName("region_code").HasMaxLength(32).IsRequired();
            rule.Property(r => r.FirstUnit).HasColumnName("first_unit");
            rule.Property(r => r.FirstPrice).HasColumnName("first_price");
            rule.Property(r => r.AdditionalUnit).HasColumnName("additional_unit");
            rule.Property(r => r.AdditionalPrice).HasColumnName("additional_price");
        });

        builder.HasIndex(f => f.SellerId).HasDatabaseName("ix_freight_templates_seller_id");
    }
}
