using Leno.Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Identity.Infrastructure.Configurations;

/// <summary>
/// RefreshToken 聚合根的 EF Core 映射配置。
/// 从 UserAuth BC 的 IRefreshTokenStore 抽象演化而来（3.6 AuthN/AuthZ 拆分）。
/// Token 唯一索引，UserId 索引支持按用户查询活跃令牌。
/// </summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Token).HasColumnName("token").HasMaxLength(128).IsRequired();
        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.IssuedAt).HasColumnName("issued_at").IsRequired();
        builder.Property(r => r.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(r => r.RevokedAt).HasColumnName("revoked_at");
        builder.Property(r => r.RevokeReason).HasColumnName("revoke_reason").HasMaxLength(64);
        builder.Property(r => r.ReplacedById).HasColumnName("replaced_by_id");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(r => r.Token).HasDatabaseName("ix_refresh_tokens_token").IsUnique();
        builder.HasIndex(r => r.UserId).HasDatabaseName("ix_refresh_tokens_user_id");
    }
}
