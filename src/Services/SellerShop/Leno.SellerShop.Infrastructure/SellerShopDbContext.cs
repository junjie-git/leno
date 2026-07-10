using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.SellerShop.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.SellerShop.Infrastructure;

/// <summary>
/// 卖家与店铺管理域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露 Shop、SellerProfile、ShopMetrics 聚合与 OutboxMessage 发件箱表的 DbSet。
/// </summary>
public sealed class SellerShopDbContext : BaseDbContext
{
    public SellerShopDbContext(DbContextOptions<SellerShopDbContext> options) : base(options)
    {
    }

    /// <summary>店铺聚合根。</summary>
    public DbSet<Shop> Shops => Set<Shop>();

    /// <summary>卖家档案聚合根。</summary>
    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();

    /// <summary>店铺运营指标聚合根。</summary>
    public DbSet<ShopMetrics> ShopMetrics => Set<ShopMetrics>();

    /// <summary>发件箱消息表，与聚合变更同事务写入。</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}
