using System.Text.Json;
using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// Operator 运营人员的 EF Core 映射配置（snake_case）。
/// 权限码集合序列化为 JSON 列。
/// </summary>
public sealed class OperatorConfiguration : IEntityTypeConfiguration<Operator>
{
    public void Configure(EntityTypeBuilder<Operator> builder)
    {
        builder.ToTable("operators");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.UserId).HasColumnName("user_id");
        builder.Property(o => o.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
        builder.Property(o => o.Role).HasColumnName("role").HasConversion<int>();
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(o => o.LastLoginAt).HasColumnName("last_login_at");

        builder.Property(o => o.Permissions)
            .HasColumnName("permissions")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(o => o.UserId).IsUnique().HasDatabaseName("ix_operators_user_id");
    }
}
