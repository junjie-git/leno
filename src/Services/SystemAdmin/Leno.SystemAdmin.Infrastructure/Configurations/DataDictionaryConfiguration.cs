using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// DataDictionary 数据字典聚合根的 EF Core 映射配置（snake_case）。
/// 字典项作为独立实体表 dictionary_items，以一对多关联维护。
/// </summary>
public sealed class DataDictionaryConfiguration : IEntityTypeConfiguration<DataDictionary>
{
    public void Configure(EntityTypeBuilder<DataDictionary> builder)
    {
        builder.ToTable("data_dictionaries");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(d => d.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasMany(d => d.Items)
            .WithOne()
            .HasForeignKey(i => i.DictionaryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.Code).IsUnique().HasDatabaseName("ix_data_dictionaries_code");
    }
}
