using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// DictionaryItem 字典项实体的 EF Core 映射配置（snake_case）。
/// 作为 DataDictionary 聚合内实体，独立持久化为 dictionary_items 表。
/// </summary>
public sealed class DictionaryItemConfiguration : IEntityTypeConfiguration<DictionaryItem>
{
    public void Configure(EntityTypeBuilder<DictionaryItem> builder)
    {
        builder.ToTable("dictionary_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.DictionaryId).HasColumnName("dictionary_id");
        builder.Property(i => i.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(i => i.Label).HasColumnName("label").HasMaxLength(128).IsRequired();
        builder.Property(i => i.Value).HasColumnName("value").HasMaxLength(256).IsRequired();
        builder.Property(i => i.SortOrder).HasColumnName("sort_order");
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(i => i.DictionaryId).HasDatabaseName("ix_dictionary_items_dictionary_id");
    }
}
