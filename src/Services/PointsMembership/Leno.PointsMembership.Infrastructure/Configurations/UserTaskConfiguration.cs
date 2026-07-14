using Leno.PointsMembership.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.PointsMembership.Infrastructure.Configurations;

/// <summary>
/// UserTask 用户任务实体的 EF Core 映射配置（snake_case）。
/// 按用户与任务建立唯一索引，防重复记录。
/// </summary>
public sealed class UserTaskConfiguration : IEntityTypeConfiguration<UserTask>
{
    public void Configure(EntityTypeBuilder<UserTask> builder)
    {
        builder.ToTable("user_tasks");
        builder.HasKey(ut => ut.Id);

        builder.Property(ut => ut.Id).HasColumnName("id");
        builder.Property(ut => ut.UserId).HasColumnName("user_id");
        builder.Property(ut => ut.TaskId).HasColumnName("task_id");
        builder.Property(ut => ut.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(ut => ut.CompletedAt).HasColumnName("completed_at");
        builder.Property(ut => ut.CompletedDate).HasColumnName("completed_date");

        builder.Property(ut => ut.CreatedAt).HasColumnName("created_at");
        builder.Property(ut => ut.UpdatedAt).HasColumnName("updated_at");
        builder.Property(ut => ut.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(ut => ut.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(ut => new { ut.UserId, ut.TaskId })
            .IsUnique()
            .HasDatabaseName("ix_user_tasks_user_id_task_id");
    }
}