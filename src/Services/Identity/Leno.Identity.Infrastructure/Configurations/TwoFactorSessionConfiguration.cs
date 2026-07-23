using Leno.Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Identity.Infrastructure.Configurations;

/// <summary>
/// TwoFactorSession 聚合根的 EF Core 映射配置。
/// 从 UserAuth BC 的 ITwoFactorTempTokenStore 抽象演化而来（3.6 AuthN/AuthZ 拆分）。
/// TempToken 唯一索引，UserId 索引支持按用户清理过期会话。
/// 注意：<see cref="TwoFactorSession.CreatedAt"/> 使用 new 关键字隐藏基类属性，
/// EF Core 通过 shadow property 配置 base.CreatedAt（审计字段由 BaseDbContext 拦截器填充），
/// TwoFactorSession.CreatedAt 显式映射为独立列。
/// </summary>
public sealed class TwoFactorSessionConfiguration : IEntityTypeConfiguration<TwoFactorSession>
{
    public void Configure(EntityTypeBuilder<TwoFactorSession> builder)
    {
        builder.ToTable("two_factor_sessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TempToken).HasColumnName("temp_token").HasMaxLength(128).IsRequired();
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(s => s.VerifiedAt).HasColumnName("verified_at");
        builder.Property(s => s.AttemptCount).HasColumnName("attempt_count").IsRequired();

        // 审计字段（基类 Entity 的 CreatedAt/UpdatedAt/CreatedBy/UpdatedBy 由 BaseDbContext 拦截器填充）
        // TwoFactorSession.CreatedAt 用 new 隐藏了基类属性，EF Core 需显式映射基类的审计字段为 shadow property
        builder.Property<DateTime>("CreatedAt").HasColumnName("audit_created_at");
        builder.Property<DateTime>("UpdatedAt").HasColumnName("audit_updated_at");
        builder.Property<string?>("CreatedBy").HasColumnName("audit_created_by").HasMaxLength(64);
        builder.Property<string?>("UpdatedBy").HasColumnName("audit_updated_by").HasMaxLength(64);

        builder.HasIndex(s => s.TempToken).HasDatabaseName("ix_two_factor_sessions_temp_token").IsUnique();
        builder.HasIndex(s => s.UserId).HasDatabaseName("ix_two_factor_sessions_user_id");
    }
}
