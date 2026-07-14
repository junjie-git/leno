using System.Text.Json;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Leno.UserAuth.Infrastructure.Configurations;

/// <summary>
/// Role 聚合根的 EF Core 映射配置（snake_case）。
/// 权限集合以 JSON 列存储。
/// </summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly ValueConverter<IReadOnlyCollection<PermissionVO>, string> PermissionsConverter = new(
        v => JsonSerializer.Serialize(v.Select(p => new PermissionJson { ResourceKey = p.ResourceKey, Description = p.Description }), JsonOptions),
        v => (IReadOnlyCollection<PermissionVO>)ConvertFromJson(v));

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(256);
        builder.Property(r => r.IsBuiltIn).HasColumnName("is_built_in");

        builder.Property(r => r.Permissions)
            .HasColumnName("permissions")
            .HasColumnType("nvarchar(max)")
            .HasConversion(PermissionsConverter);

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(r => r.Name).HasDatabaseName("ix_roles_name").IsUnique();
    }

    private static List<PermissionVO> ConvertFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<PermissionVO>();
        }

        var list = JsonSerializer.Deserialize<List<PermissionJson>>(json, JsonOptions);
        if (list == null)
        {
            return new List<PermissionVO>();
        }

        return list.Select(p => new PermissionVO(p.ResourceKey) { Description = p.Description }).ToList();
    }

    private sealed class PermissionJson
    {
        public string ResourceKey { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}