using System.Text.Json;
using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// Menu 菜单聚合根的 EF Core 映射配置（snake_case 表名）。
/// Roles 字段以 JSON 数组序列化存储；Type 用 byte 转换以匹配 TINYINT 列。
/// </summary>
public sealed class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    private static readonly JsonSerializerOptions RolesJsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("menus");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.ParentId).HasColumnName("parent_id");
        builder.Property(m => m.Name).HasColumnName("name").HasMaxLength(32).IsRequired();
        builder.Property(m => m.Type).HasColumnName("type").HasConversion(v => (byte)v, v => (MenuType)v);
        builder.Property(m => m.Path).HasColumnName("path").HasMaxLength(256);
        builder.Property(m => m.Component).HasColumnName("component").HasMaxLength(256);
        builder.Property(m => m.Icon).HasColumnName("icon").HasMaxLength(64);
        builder.Property(m => m.Sort).HasColumnName("sort").HasDefaultValue(0);
        builder.Property(m => m.Permission).HasColumnName("permission").HasMaxLength(64);
        builder.Property(m => m.Roles)
            .HasColumnName("roles")
            .HasMaxLength(256)
            .HasConversion(
                v => JsonSerializer.Serialize(v, RolesJsonOptions),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(v, RolesJsonOptions) ?? new List<string>());
        builder.Property(m => m.Visible).HasColumnName("visible").HasDefaultValue(true);
        builder.Property(m => m.Cache).HasColumnName("cache").HasDefaultValue(false);

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(m => m.ParentId).HasDatabaseName("ix_menus_parent_id");
        builder.HasIndex(m => new { m.Type, m.Visible }).HasDatabaseName("ix_menus_type_visible");
    }
}
