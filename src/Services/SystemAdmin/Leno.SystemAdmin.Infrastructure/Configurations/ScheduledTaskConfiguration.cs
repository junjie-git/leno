using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// ScheduledTask 定时任务的 EF Core 映射配置（snake_case）。
/// 参数 JSON 直接以字符串列持久化。
/// </summary>
public sealed class ScheduledTaskConfiguration : IEntityTypeConfiguration<ScheduledTask>
{
    public void Configure(EntityTypeBuilder<ScheduledTask> builder)
    {
        builder.ToTable("scheduled_tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(t => t.JobType).HasColumnName("job_type").HasMaxLength(256).IsRequired();
        builder.Property(t => t.CronExpression).HasColumnName("cron_expression").HasMaxLength(128).IsRequired();
        builder.Property(t => t.Parameters).HasColumnName("parameters").HasColumnType("nvarchar(max)");
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(t => t.LastRunAt).HasColumnName("last_run_at");
        builder.Property(t => t.LastRunStatus).HasColumnName("last_run_status").HasConversion<int>();
        builder.Property(t => t.NextRunAt).HasColumnName("next_run_at");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(t => t.Status).HasDatabaseName("ix_scheduled_tasks_status");
    }
}
