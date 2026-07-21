using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// IndexRebuildTask 索引重建任务的 EF Core 映射配置（snake_case）。
/// 按 (target_context, index_name, status) 建立复合索引以支持冲突检测与查询。
/// </summary>
public sealed class IndexRebuildTaskConfiguration : IEntityTypeConfiguration<IndexRebuildTask>
{
    public void Configure(EntityTypeBuilder<IndexRebuildTask> builder)
    {
        builder.ToTable("index_rebuild_tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TargetContext).HasColumnName("target_context").HasMaxLength(128).IsRequired();
        builder.Property(t => t.IndexName).HasColumnName("index_name").HasMaxLength(256).IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(t => t.TriggeredBy).HasColumnName("triggered_by").HasMaxLength(64).IsRequired();
        builder.Property(t => t.Progress).HasColumnName("progress");
        builder.Property(t => t.EsTaskId).HasColumnName("es_task_id").HasMaxLength(256);
        builder.Property(t => t.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(t => t.RetryCount).HasColumnName("retry_count");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.StartedAt).HasColumnName("started_at");
        builder.Property(t => t.CompletedAt).HasColumnName("completed_at");

        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(t => new { t.TargetContext, t.IndexName, t.Status })
            .HasDatabaseName("ix_index_rebuild_tasks_context_index_status");
    }
}