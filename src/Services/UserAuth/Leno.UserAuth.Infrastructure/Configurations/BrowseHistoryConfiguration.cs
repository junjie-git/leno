using Leno.UserAuth.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.UserAuth.Infrastructure.Configurations;

/// <summary>
/// BrowseHistory 聚合根的 EF Core 映射配置。表名 snake_case；UserId 索引；UserId + SpuId 索引（幂等查询用）。
/// </summary>
public sealed class BrowseHistoryConfiguration : IEntityTypeConfiguration<BrowseHistory>
{
    public void Configure(EntityTypeBuilder<BrowseHistory> builder)
    {
        builder.ToTable("browse_histories");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasColumnName("id");
        builder.Property(h => h.UserId).HasColumnName("user_id");
        builder.Property(h => h.SpuId).HasColumnName("spu_id");
        builder.Property(h => h.SkuId).HasColumnName("sku_id");
        builder.Property(h => h.ViewedAt).HasColumnName("viewed_at");

        builder.Property(h => h.CreatedAt).HasColumnName("created_at");
        builder.Property(h => h.UpdatedAt).HasColumnName("updated_at");
        builder.Property(h => h.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(h => h.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(h => h.UserId).HasDatabaseName("ix_browse_histories_user_id");
        // 用户 + SPU 复合索引：支持 FindLatestByUserAndSpuAsync 幂等查询
        builder.HasIndex(h => new { h.UserId, h.SpuId, h.ViewedAt })
            .HasDatabaseName("ix_browse_histories_user_spu_viewed_at");
    }
}
