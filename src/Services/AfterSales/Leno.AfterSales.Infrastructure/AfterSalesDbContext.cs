using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.AfterSales.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.AfterSales.Infrastructure;

/// <summary>
/// 售后域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露售后聚合与 OutboxMessage 发件箱表的 DbSet。
/// </summary>
public sealed class AfterSalesDbContext : BaseDbContext
{
    public AfterSalesDbContext(DbContextOptions<AfterSalesDbContext> options) : base(options)
    {
    }

    /// <summary>售后单聚合根。</summary>
    public DbSet<AfterSalesOrder> AfterSalesOrders => Set<AfterSalesOrder>();
}
