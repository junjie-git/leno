using Leno.Infrastructure.Persistence;
using Leno.Points.Domain.Aggregates.PointsAccount;
using Leno.Points.Domain.Aggregates.PointsExchange;
using Leno.Points.Domain.Aggregates.PointsFlow;
using Microsoft.EntityFrameworkCore;
using PointsAccountAggregate = Leno.Points.Domain.Aggregates.PointsAccount.PointsAccount;
using PointsExchangeAggregate = Leno.Points.Domain.Aggregates.PointsExchange.PointsExchange;

namespace Leno.Points.Infrastructure;

/// <summary>
/// Points BC DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露积分账户、积分流水、积分兑换聚合与 OutboxMessage 发件箱表的 DbSet。
/// 数据库：points_db（独立于旧 points_membership_db，支持 BC 独立伸缩）。
/// </summary>
public sealed class PointsDbContext : BaseDbContext
{
    public PointsDbContext(DbContextOptions<PointsDbContext> options) : base(options)
    {
    }

    /// <summary>积分账户聚合根。</summary>
    public DbSet<PointsAccountAggregate> PointsAccounts => Set<PointsAccountAggregate>();

    /// <summary>积分冻结明细子实体，隶属积分账户聚合。</summary>
    public DbSet<FrozenPoints> PointsFrozenEntries => Set<FrozenPoints>();

    /// <summary>积分流水实体，记录账户单笔变动明细。</summary>
    public DbSet<PointsFlow> PointsFlows => Set<PointsFlow>();

    /// <summary>积分兑换聚合根。</summary>
    public DbSet<PointsExchangeAggregate> PointsExchanges => Set<PointsExchangeAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PointsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
