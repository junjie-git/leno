using Leno.UserCenter.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.UserCenter.Infrastructure.Configurations;

/// <summary>
/// Favorite 聚合根的 EF Core 映射配置。表名 snake_case；UserId + SpuId 复合唯一索引防止重复收藏。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("favorites");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.UserId).HasColumnName("user_id");
        builder.Property(f => f.SpuId).HasColumnName("spu_id");
        builder.Property(f => f.FavoritedAt).HasColumnName("favorited_at");

        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        builder.Property(f => f.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(f => f.UserId).HasDatabaseName("ix_favorites_user_id");
        // 用户 + SPU 复合唯一索引：防止同一用户重复收藏同一 SPU
        builder.HasIndex(f => new { f.UserId, f.SpuId })
            .HasDatabaseName("ix_favorites_user_spu")
            .IsUnique();
    }
}
