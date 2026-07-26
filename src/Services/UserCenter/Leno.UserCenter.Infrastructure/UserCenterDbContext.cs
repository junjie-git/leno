using Leno.Infrastructure.Persistence;
using Leno.UserCenter.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.UserCenter.Infrastructure;

/// <summary>
/// 用户中心域 DbContext，承载从 UserAuth 域拆分出的 Address/Favorite/BrowseHistory/NotificationPreferences 聚合。
/// 继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// </summary>
public sealed class UserCenterDbContext : BaseDbContext
{
    public UserCenterDbContext(DbContextOptions<UserCenterDbContext> options) : base(options)
    {
    }

    /// <summary>收货地址聚合根（从 UserAuth 域迁入）。</summary>
    public DbSet<Address> Addresses => Set<Address>();

    /// <summary>商品收藏聚合根（从 UserAuth 域迁入）。</summary>
    public DbSet<Favorite> Favorites => Set<Favorite>();

    /// <summary>浏览历史聚合根（从 UserAuth 域迁入）。</summary>
    public DbSet<BrowseHistory> BrowseHistories => Set<BrowseHistory>();

    /// <summary>通知偏好聚合根（从 UserAuth 域迁入，与 Notification 域共享表，参见 Spec §4.3.5）。</summary>
    public DbSet<NotificationPreferences> NotificationPreferences => Set<NotificationPreferences>();
}
