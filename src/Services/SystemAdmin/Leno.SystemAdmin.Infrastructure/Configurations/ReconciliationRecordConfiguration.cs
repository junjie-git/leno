using System.Text.Json;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// ReconciliationRecord 对账记录的 EF Core 映射配置（snake_case）。
/// Snapshot 存储为 JSON 列。
/// </summary>
public sealed class ReconciliationRecordConfiguration : IEntityTypeConfiguration<ReconciliationRecord>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<ReconciliationRecord> builder)
    {
        builder.ToTable("reconciliation_records");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.ReportType).HasColumnName("report_type").HasConversion<int>();
        builder.Property(r => r.ReconciledAt).HasColumnName("reconciled_at");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(r => r.AlertTriggered).HasColumnName("alert_triggered");
        builder.Property(r => r.CorrectionTriggered).HasColumnName("correction_triggered");

        builder.Property(r => r.Version).HasColumnName("version").IsRowVersion();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // StatisticsSnapshot as JSON column
        builder.Property(r => r.Snapshot)
            .HasColumnName("snapshot")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<StatisticsSnapshot>(v, JsonOptions) ?? new StatisticsSnapshot(
                    ReportType.OrderGmv,
                    new ReportPeriod(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow),
                    new List<MetricItem>(),
                    new List<MetricItem>(),
                    new List<MetricDiscrepancy>()));

        builder.HasIndex(r => new { r.ReportType, r.ReconciledAt })
            .HasDatabaseName("ix_reconciliation_records_type_reconciled_at");
    }
}