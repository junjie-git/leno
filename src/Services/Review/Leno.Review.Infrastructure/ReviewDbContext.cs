using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.Review.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using ReviewAggregate = Leno.Review.Domain.Aggregates.Review;

namespace Leno.Review.Infrastructure;

/// <summary>
/// 评价域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露评价聚合与 OutboxMessage 发件箱表的 DbSet。
/// </summary>
public sealed class ReviewDbContext : BaseDbContext
{
    public ReviewDbContext(DbContextOptions<ReviewDbContext> options) : base(options)
    {
    }

    /// <summary>评价聚合根。</summary>
    public DbSet<ReviewAggregate> Reviews => Set<ReviewAggregate>();
}
