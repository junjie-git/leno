using Leno.UserAuth.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.UserAuth.Infrastructure.Configurations;

/// <summary>
/// AuditLog 聚合根的 EF Core 映射配置。审计日志只追加，无更新与删除。
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.OperatorId).HasColumnName("operator_id");
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
        builder.Property(a => a.ResourceType).HasColumnName("resource_type").HasMaxLength(64).IsRequired();
        builder.Property(a => a.ResourceId).HasColumnName("resource_id").HasMaxLength(64);
        builder.Property(a => a.BeforeSnapshot).HasColumnName("before_snapshot");
        builder.Property(a => a.AfterSnapshot).HasColumnName("after_snapshot");
        builder.Property(a => a.OperatedAt).HasColumnName("operated_at");
        builder.Property(a => a.Ip).HasColumnName("ip").HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
        builder.Property(a => a.TraceId).HasColumnName("trace_id").HasMaxLength(64);

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.HasIndex(a => a.OperatorId).HasDatabaseName("ix_audit_logs_operator_id");
        builder.HasIndex(a => a.OperatedAt).HasDatabaseName("ix_audit_logs_operated_at");
    }
}
