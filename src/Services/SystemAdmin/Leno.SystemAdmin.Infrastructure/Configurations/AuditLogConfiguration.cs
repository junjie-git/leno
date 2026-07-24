using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// AuditLog 审计日志的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.OperatorId).HasColumnName("operator_id");
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(128).IsRequired();
        builder.Property(a => a.ResourceType).HasColumnName("resource_type").HasMaxLength(64).IsRequired();
        builder.Property(a => a.ResourceId).HasColumnName("resource_id").HasMaxLength(64).IsRequired();
        builder.Property(a => a.RequestSummary).HasColumnName("request_summary").HasMaxLength(2000);
        builder.Property(a => a.ResponseStatus).HasColumnName("response_status");
        builder.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(a => a.TraceId).HasColumnName("trace_id").HasMaxLength(64);
        builder.Property(a => a.OccurredAt).HasColumnName("occurred_at");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 多租户预留扩展位（4.7）：tenant_id 列声明（nullable，默认 null = 全局数据）。
        // BaseDbContext.OnModelCreating 会统一为 ITenantEntity 实体配置此列 + 全局查询过滤器，
        // 此处显式声明以明确 AuditLog 支持多租户扩展位，并追加索引便于按租户查询。
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired(false);
        builder.HasIndex(a => a.TenantId).HasDatabaseName("ix_audit_logs_tenant_id");

        builder.HasIndex(a => a.OperatorId).HasDatabaseName("ix_audit_logs_operator_id");
        builder.HasIndex(a => a.OccurredAt).HasDatabaseName("ix_audit_logs_occurred_at");
        builder.HasIndex(a => a.ResourceType).HasDatabaseName("ix_audit_logs_resource_type");
    }
}
