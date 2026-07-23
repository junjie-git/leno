using Leno.Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.Identity.Infrastructure.Configurations;

/// <summary>
/// User 聚合根的 EF Core 映射配置。
/// 表名 snake_case；Username/Email/Phone 唯一索引（Email/Phone 过滤 NULL）。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）：移除 Roles owned collection（角色已迁至 AccessControl BC）。
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(32).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256);
        builder.Property(u => u.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
        // password_hash 扩列至 256，为未来切换 Argon2id（典型 96+ 字符）预留空间
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(256);
        builder.Property(u => u.Nickname).HasColumnName("nickname").HasMaxLength(32).IsRequired();
        builder.Property(u => u.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(512);
        builder.Property(u => u.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(u => u.DefaultAddressId).HasColumnName("default_address_id");
        builder.Property(u => u.FailedLoginCount).HasColumnName("failed_login_count");
        builder.Property(u => u.LockedUntil).HasColumnName("locked_until");
        builder.Property(u => u.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        builder.Property(u => u.TwoFactorSecret).HasColumnName("two_factor_secret").HasMaxLength(256);

        // 乐观并发控制：FailedLoginCount 并发累加时通过 RowVersion 检测冲突
        builder.Property(u => u.RowVersion).HasColumnName("row_version").IsRowVersion();

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        // 外部登录绑定集合：owned collection，落 user_external_logins 表
        builder.OwnsMany(u => u.ExternalLogins, owned =>
        {
            owned.ToTable("user_external_logins");
            owned.HasKey("UserId", nameof(ExternalLogin.Provider));
            owned.WithOwner().HasForeignKey("UserId");
            owned.Property<Guid>("UserId").HasColumnName("user_id");
            owned.Property(el => el.Provider).HasColumnName("provider").HasMaxLength(32).IsRequired();
            owned.Property(el => el.ProviderUserId).HasColumnName("provider_user_id").HasMaxLength(256).IsRequired();
            owned.Property(el => el.Email).HasColumnName("email").HasMaxLength(256);
            owned.Property(el => el.Name).HasColumnName("name").HasMaxLength(128);
            owned.Property(el => el.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(512);
            owned.Property(el => el.LinkedAt).HasColumnName("linked_at").IsRequired();

            owned.HasIndex(el => new { el.Provider, el.ProviderUserId })
                .HasDatabaseName("ix_user_external_logins_provider_user_id")
                .IsUnique();
        });

        builder.HasIndex(u => u.Username).HasDatabaseName("ix_users_username").IsUnique();
        builder.HasIndex(u => u.Email).HasDatabaseName("ix_users_email").IsUnique()
            .HasFilter("[email] IS NOT NULL");
        builder.HasIndex(u => u.PhoneNumber).HasDatabaseName("ix_users_phone_number").IsUnique()
            .HasFilter("[phone_number] IS NOT NULL");
    }
}
