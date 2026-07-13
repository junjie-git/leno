using System.Text.Json;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// DashboardReport 运营数据看板报表的 EF Core 映射配置（snake_case）。
/// Metrics 存储为 JSON 列。
/// </summary>
public sealed class DashboardReportConfiguration : IEntityTypeConfiguration<DashboardReport>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<DashboardReport> builder)
    {
        builder.ToTable("dashboard_reports");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.ReportType).HasColumnName("report_type").HasConversion<int>();
        builder.Property(r => r.Granularity).HasColumnName("granularity").HasMaxLength(16).IsRequired();
        builder.Property(r => r.GeneratedAt).HasColumnName("generated_at");
        builder.Property(r => r.DataVersion).HasColumnName("data_version");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // ReportPeriod as owned entity
        builder.OwnsOne(r => r.Period, period =>
        {
            period.Property(p => p.Start).HasColumnName("period_start");
            period.Property(p => p.End).HasColumnName("period_end");
        });

        // Metrics as JSON column
        builder.Property(r => r.Metrics)
            .HasColumnName("metrics")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<MetricItem>>(v, JsonOptions) ?? new List<MetricItem>());

        builder.HasIndex(r => new { r.ReportType, r.GeneratedAt })
            .HasDatabaseName("ix_dashboard_reports_type_generated_at");
    }
}