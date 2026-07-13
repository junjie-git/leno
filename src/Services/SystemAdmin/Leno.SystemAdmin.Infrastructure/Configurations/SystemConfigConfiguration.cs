using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// SystemConfig 系统配置的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> builder)
    {
        builder.ToTable("system_configs");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Key).HasColumnName("key").HasMaxLength(128).IsRequired();
        builder.Property(c => c.Value).HasColumnName("value").HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(c => c.Group).HasColumnName("group").HasMaxLength(64).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(c => c.IsEncrypted).HasColumnName("is_encrypted");
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(c => c.Key).IsUnique().HasDatabaseName("ix_system_configs_key");
        builder.HasIndex(c => c.Group).HasDatabaseName("ix_system_configs_group");
    }
}
