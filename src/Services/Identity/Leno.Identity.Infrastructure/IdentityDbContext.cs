using Leno.Identity.Domain.Aggregates;
using Leno.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Leno.Identity.Infrastructure;

/// <summary>
/// Identity BC DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露 User、OAuthClient、RefreshToken、TwoFactorSession 聚合的 DbSet。
/// 从 UserAuth BC 拆分而来（3.6 AuthN/AuthZ 拆分）：移除 Role、Address、AuditLog（分别迁至 AccessControl BC 与 SystemAdmin BC）。
/// </summary>
public sealed class IdentityDbContext : BaseDbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    /// <summary>用户聚合根（Identity BC，仅承载身份凭证，不含角色）。</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>OAuth2 客户端配置聚合根。</summary>
    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();

    /// <summary>刷新令牌聚合根。</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>双因子认证会话聚合根。</summary>
    public DbSet<TwoFactorSession> TwoFactorSessions => Set<TwoFactorSession>();
}
