using Leno.Product.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Product.Infrastructure.Configurations;

/// <summary>
/// Category 聚合根的 EF Core 映射配置（snake_case）。
/// ParentId 自引用支撑多级分类树；状态以整型落库。
/// </summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(c => c.ParentId).HasColumnName("parent_id");
        builder.Property(c => c.Level).HasColumnName("level");
        builder.Property(c => c.SortOrder).HasColumnName("sort_order");
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.HasIndex(c => c.ParentId).HasDatabaseName("ix_categories_parent_id");
    }
}
