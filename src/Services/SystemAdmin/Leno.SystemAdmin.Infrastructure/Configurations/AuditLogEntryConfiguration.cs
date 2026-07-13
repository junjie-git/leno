using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// AuditLogEntry 跨域审计日志条目的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log_entries");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(a => a.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
        builder.Property(a => a.AggregateId).HasColumnName("aggregate_id");
        builder.Property(a => a.Module).HasColumnName("module").HasMaxLength(64).IsRequired();
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(128).IsRequired();
        builder.Property(a => a.OperatorId).HasColumnName("operator_id");
        builder.Property(a => a.OperatorName).HasColumnName("operator_name").HasMaxLength(128);
        builder.Property(a => a.RequestSummary).HasColumnName("request_summary").HasMaxLength(2000);
        builder.Property(a => a.Timestamp).HasColumnName("timestamp");
        builder.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(64);

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(a => a.EventId).HasDatabaseName("ix_audit_log_entries_event_id").IsUnique();
        builder.HasIndex(a => a.Module).HasDatabaseName("ix_audit_log_entries_module");
        builder.HasIndex(a => a.Action).HasDatabaseName("ix_audit_log_entries_action");
        builder.HasIndex(a => a.Timestamp).HasDatabaseName("ix_audit_log_entries_timestamp");
        builder.HasIndex(a => a.OperatorId).HasDatabaseName("ix_audit_log_entries_operator_id");
    }
}