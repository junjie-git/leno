using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.ReviewAfterSales.Infrastructure;

/// <summary>
/// 评价与售后域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露评价、售后聚合与 OutboxMessage 发件箱表的 DbSet。
/// </summary>
public sealed class ReviewAfterSalesDbContext : BaseDbContext
{
    public ReviewAfterSalesDbContext(DbContextOptions<ReviewAfterSalesDbContext> options) : base(options)
    {
    }

    /// <summary>评价聚合根。</summary>
    public DbSet<Review> Reviews => Set<Review>();

    /// <summary>售后单聚合根。</summary>
    public DbSet<AfterSales> AfterSales => Set<AfterSales>();

    /// <summary>发件箱消息表，与聚合变更同事务写入。</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}
