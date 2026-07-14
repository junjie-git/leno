using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// FeatureFlag 特性开关的 EF Core 映射配置（snake_case）。
/// 规则 JSON 直接以字符串列持久化，领域层保持透明不解析。
/// </summary>
public sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("feature_flags");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.Key).HasColumnName("key").HasMaxLength(128).IsRequired();
        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(f => f.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(f => f.IsEnabled).HasColumnName("is_enabled");
        builder.Property(f => f.Strategy).HasColumnName("strategy").HasConversion<int>();
        builder.Property(f => f.Rules).HasColumnName("rules").HasColumnType("nvarchar(max)");

        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        builder.Property(f => f.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(f => f.Key).IsUnique().HasDatabaseName("ix_feature_flags_key");
    }
}
