using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leno.SystemAdmin.Infrastructure.Configurations;

/// <summary>
/// LoginLog 登录日志聚合根的 EF Core 映射配置（snake_case 表名）。
/// 仅追加，无 Update/Delete；Result 用 byte 转换以匹配 TINYINT 列。
/// </summary>
public sealed class LoginLogConfiguration : IEntityTypeConfiguration<LoginLog>
{
    public void Configure(EntityTypeBuilder<LoginLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("login_logs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(l => l.UserId).HasColumnName("user_id");
        builder.Property(l => l.IpAddress).HasColumnName("ip_address").HasMaxLength(64).IsRequired();
        builder.Property(l => l.GeoLocation).HasColumnName("geo_location").HasMaxLength(128);
        builder.Property(l => l.Browser).HasColumnName("browser").HasMaxLength(64).IsRequired();
        builder.Property(l => l.Os).HasColumnName("os").HasMaxLength(64).IsRequired();
        builder.Property(l => l.Result).HasColumnName("result").HasConversion(v => (byte)v, v => (LoginResult)v);
        builder.Property(l => l.FailureReason).HasColumnName("failure_reason").HasMaxLength(64);
        builder.Property(l => l.DurationMs).HasColumnName("duration_ms");
        builder.Property(l => l.UserAgent).HasColumnName("user_agent").HasMaxLength(512).IsRequired();
        builder.Property(l => l.DeviceFingerprint).HasColumnName("device_fingerprint").HasMaxLength(128);
        builder.Property(l => l.RefererUrl).HasColumnName("referer_url").HasMaxLength(512);
        builder.Property(l => l.TraceId).HasColumnName("trace_id").HasMaxLength(64).IsRequired();
        builder.Property(l => l.LoginAt).HasColumnName("login_at");

        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);

        builder.HasIndex(l => l.LoginAt).IsDescending().HasDatabaseName("ix_login_logs_login_at");
        builder.HasIndex(l => new { l.Username, l.LoginAt }).IsDescending().HasDatabaseName("ix_login_logs_username_login_at");
        builder.HasIndex(l => new { l.Result, l.LoginAt }).IsDescending().HasDatabaseName("ix_login_logs_result_login_at");
    }
}
