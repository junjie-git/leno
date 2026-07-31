using Leno.SellerShop.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SellerShop.Infrastructure.Configurations;

/// <summary>
/// ExportTask 聚合根的 EF Core 映射配置。
/// 表名 snake_case；ShopId+Status 复合索引支撑按店铺分页过滤；Status+CreatedAt 复合索引支撑后台轮询最早处理中任务。
/// CreatedBy/UpdatedBy 为 string? 审计字段（nvarchar(64)），与 ShopConfiguration 保持一致。
/// </summary>
public sealed class ExportTaskConfiguration : IEntityTypeConfiguration<ExportTask>
{
    public void Configure(EntityTypeBuilder<ExportTask> builder)
    {
        builder.ToTable("export_tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.ShopId).HasColumnName("shop_id");
        builder.Property(t => t.SellerId).HasColumnName("seller_id");
        builder.Property(t => t.ReportType).HasColumnName("report_type").HasMaxLength(64).IsRequired();
        builder.Property(t => t.StartDate).HasColumnName("start_date");
        builder.Property(t => t.EndDate).HasColumnName("end_date");
        builder.Property(t => t.Format).HasColumnName("format").HasMaxLength(16).IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(t => t.RecordCount).HasColumnName("record_count");
        builder.Property(t => t.FileSize).HasColumnName("file_size");
        builder.Property(t => t.FilePath).HasColumnName("file_path").HasMaxLength(512);
        builder.Property(t => t.ErrorMessage).HasColumnName("error_message").HasMaxLength(1024);
        builder.Property(t => t.CompletedAt).HasColumnName("completed_at");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(t => new { t.ShopId, t.Status }).HasDatabaseName("ix_export_tasks_shop_id_status");
        builder.HasIndex(t => new { t.Status, t.CreatedAt }).HasDatabaseName("ix_export_tasks_status_created_at");
    }
}
