using Leno.AccessControl.Domain.Aggregates;
using Leno.AccessControl.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.AccessControl.Infrastructure.Configurations;

/// <summary>
/// UserRoleAssignment 聚合根的 EF Core 映射配置（snake_case）。
/// 从 UserAuth BC 的 User._roles owned collection 拆出为独立聚合（3.6 AuthN/AuthZ 拆分）。
/// 唯一索引：(user_id, role) WHERE is_active = 1，确保同一用户同一角色仅一条生效记录。
/// </summary>
public sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("user_role_assignments");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(u => u.Role).HasColumnName("role").HasConversion<int>().IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(u => u.AssignedAt).HasColumnName("assigned_at").IsRequired();
        builder.Property(u => u.RevokedAt).HasColumnName("revoked_at");
        builder.Property(u => u.OperatorId).HasColumnName("operator_id");

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 查询索引：按用户查询生效角色（GetUserRoles RPC 高频调用）
        builder.HasIndex(u => new { u.UserId, u.IsActive })
            .HasDatabaseName("ix_user_role_assignments_user_id_is_active");

        // 唯一约束：同一用户同一角色仅一条生效记录（部分索引，仅 is_active=1 约束）
        builder.HasIndex(u => new { u.UserId, u.Role })
            .HasDatabaseName("ix_user_role_assignments_user_id_role_unique")
            .IsUnique()
            .HasFilter("[is_active] = 1");
    }
}
