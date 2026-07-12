using Leno.PointsMembership.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.PointsMembership.Infrastructure.Configurations;

/// <summary>
/// TaskDefinition 任务定义聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class TaskConfiguration : IEntityTypeConfiguration<TaskDefinition>
{
    public void Configure(EntityTypeBuilder<TaskDefinition> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Type).HasColumnName("type").HasConversion<int>();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(512);
        builder.Property(t => t.RewardPoints).HasColumnName("reward_points");
        builder.Property(t => t.CompletionCondition).HasColumnName("completion_condition").HasMaxLength(256);
        builder.Property(t => t.IsDaily).HasColumnName("is_daily");
        builder.Property(t => t.IsOneTime).HasColumnName("is_one_time");
        builder.Property(t => t.IsEnabled).HasColumnName("is_enabled");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.Property(t => t.Version).HasColumnName("version").IsRowVersion();

        builder.HasIndex(t => t.Type).IsUnique().HasDatabaseName("ix_tasks_type");
    }
}