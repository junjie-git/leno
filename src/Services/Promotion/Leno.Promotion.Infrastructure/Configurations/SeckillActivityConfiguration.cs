using Leno.Promotion.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Promotion.Infrastructure.Configurations;

/// <summary>
/// SeckillActivity 秒杀活动聚合根的 EF Core 映射配置（snake_case）。
/// </summary>
public sealed class SeckillActivityConfiguration : IEntityTypeConfiguration<SeckillActivity>
{
    public void Configure(EntityTypeBuilder<SeckillActivity> builder)
    {
        builder.ToTable("seckill_activities");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SpuId).HasColumnName("spu_id");
        builder.Property(s => s.SkuId).HasColumnName("sku_id");
        builder.Property(s => s.SeckillPrice).HasColumnName("seckill_price").HasPrecision(18, 2);
        builder.Property(s => s.OriginalPrice).HasColumnName("original_price").HasPrecision(18, 2);
        builder.Property(s => s.TotalStock).HasColumnName("total_stock");
        builder.Property(s => s.AvailableStock).HasColumnName("available_stock");
        builder.Property(s => s.LimitPerUser).HasColumnName("limit_per_user");
        builder.Property(s => s.StartTime).HasColumnName("start_time");
        builder.Property(s => s.EndTime).HasColumnName("end_time");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(s => s.Status).HasDatabaseName("ix_seckill_activities_status");
        builder.HasIndex(s => s.SkuId).HasDatabaseName("ix_seckill_activities_sku_id");
    }
}
