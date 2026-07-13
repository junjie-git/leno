using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// OperationLog 操作日志的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class OperationLogConfiguration : IEntityTypeConfiguration<OperationLog>
{
    public void Configure(EntityTypeBuilder<OperationLog> builder)
    {
        builder.ToTable("operation_logs");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.OperatorId).HasColumnName("operator_id");
        builder.Property(o => o.OperationType).HasColumnName("operation_type").HasMaxLength(64).IsRequired();
        builder.Property(o => o.Module).HasColumnName("module").HasMaxLength(64).IsRequired();
        builder.Property(o => o.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(o => o.BeforeSnapshot).HasColumnName("before_snapshot").HasMaxLength(4000);
        builder.Property(o => o.AfterSnapshot).HasColumnName("after_snapshot").HasMaxLength(4000);
        builder.Property(o => o.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(o => o.OccurredAt).HasColumnName("occurred_at");

        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(o => o.OperatorId).HasDatabaseName("ix_operation_logs_operator_id");
        builder.HasIndex(o => o.OccurredAt).HasDatabaseName("ix_operation_logs_occurred_at");
    }
}
