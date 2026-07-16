using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.Order.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure;

/// <summary>
/// 订单域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露订单、库存预占、物流公司、运费模板聚合与 OutboxMessage 发件箱表的 DbSet。
/// OrderItem 作为 Order 聚合的 owned collection 持久化，无需独立 DbSet。
/// </summary>
public sealed class OrderDbContext : BaseDbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    /// <summary>订单聚合根。</summary>
    public DbSet<OrderAggregate> Orders => Set<OrderAggregate>();

    /// <summary>库存预占聚合根。</summary>
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    /// <summary>库存预占回滚补偿记录聚合根（T18），由后台任务定期重试 Pending 记录。</summary>
    public DbSet<StockReservationCompensation> StockReservationCompensations => Set<StockReservationCompensation>();

    /// <summary>物流公司聚合根。</summary>
    public DbSet<LogisticsCompany> LogisticsCompanies => Set<LogisticsCompany>();

    /// <summary>运费模板聚合根。</summary>
    public DbSet<FreightTemplate> FreightTemplates => Set<FreightTemplate>();

    /// <summary>发件箱消息表，与聚合变更同事务写入。</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}
