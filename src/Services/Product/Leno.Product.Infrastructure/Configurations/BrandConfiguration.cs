using Leno.Product.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Product.Infrastructure.Configurations;

/// <summary>
/// Brand 聚合根的 EF Core 映射配置（snake_case）。
/// 状态以整型落库；名称建立普通索引支撑关键词检索。
/// </summary>
public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(b => b.Logo).HasColumnName("logo").HasMaxLength(512);
        builder.Property(b => b.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
        builder.Property(b => b.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(b => b.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.Property(b => b.Version).HasColumnName("version").IsRowVersion();

        builder.HasIndex(b => b.Name).HasDatabaseName("ix_brands_name");
    }
}
