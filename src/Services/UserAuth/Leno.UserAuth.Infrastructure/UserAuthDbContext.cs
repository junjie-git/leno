using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.UserAuth.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserAuth.Infrastructure;

/// <summary>
/// 用户与认证授权域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露 User、Address、AuditLog 聚合与 OutboxMessage 发件箱表的 DbSet。
/// </summary>
public sealed class UserAuthDbContext : BaseDbContext
{
    public UserAuthDbContext(DbContextOptions<UserAuthDbContext> options) : base(options)
    {
    }

    /// <summary>用户聚合根。</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>收货地址聚合根。</summary>
    public DbSet<Address> Addresses => Set<Address>();

    /// <summary>审计日志聚合根（只追加）。</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>角色权限聚合根。</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>OAuth2 客户端配置聚合根。</summary>
    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();

    /// <summary>通知偏好聚合根。</summary>
    public DbSet<NotificationPreferences> NotificationPreferences => Set<NotificationPreferences>();

    /// <summary>商品收藏聚合根。</summary>
    public DbSet<Favorite> Favorites => Set<Favorite>();

    /// <summary>浏览历史聚合根。</summary>
    public DbSet<BrowseHistory> BrowseHistories => Set<BrowseHistory>();
}
