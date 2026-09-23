using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.Infrastructure.ReadModel;
using Leno.Order.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Infrastructure;

/// <summary>
/// 订单域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露订单、物流公司、运费模板聚合与 OutboxMessage 发件箱表的 DbSet。
/// OrderItem 作为 Order 聚合的 owned collection 持久化，无需独立 DbSet。
/// 3.2：暴露 <see cref="OrderSagaStates"/> DbSet，由 MassTransit EF Core Saga 持久化 OrderSagaState（崩溃恢复）。
/// 3.3：暴露 <see cref="OrderPaymentProcesses"/> DbSet，由 Process Manager 读写 OrderPaymentProcessState（支付后编排状态）。
/// 3.12：暴露 <see cref="ReadModelSnapshots"/> DbSet，由 <see cref="SqlSnapshotStore{TContext}"/> 持久化读模型快照（快照+增量回放）。
/// 双轨下线 DEC-4（2026-09-22）：库存预占/补偿聚合与表已整体迁出 —— 库存以 Inventory BC 为唯一权威，
/// Order 不再持有任何库存存储（库存交互经 IInventoryGateway）。
/// 双轨下线 D1/D2（2026-09-23）：Saga 状态机与 Process Manager 两个未完成原型已删除，
/// 其状态表（order_saga_states / order_payment_processes）随迁移移除。
/// </summary>
public sealed class OrderDbContext : BaseDbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    /// <summary>订单聚合根。</summary>
    public DbSet<OrderAggregate> Orders => Set<OrderAggregate>();

    /// <summary>物流公司聚合根。</summary>
    public DbSet<LogisticsCompany> LogisticsCompanies => Set<LogisticsCompany>();

    /// <summary>运费模板聚合根。</summary>
    public DbSet<FreightTemplate> FreightTemplates => Set<FreightTemplate>();

    /// <summary>
    /// 订单 Saga 状态机实例集合（3.2），持久化到 order_saga_states 表。
    /// 由 MassTransit EF Core Saga Repository 读写，服务崩溃重启后从本表恢复 Saga 状态。
    /// </summary>
    /// <summary>
    /// 读模型快照集合（3.12），持久化到 read_model_snapshots 表。
    /// 由 <see cref="SqlSnapshotStore{TContext}"/> 读写，支持 CQRS 读模型快照重建与增量回放。
    /// </summary>
    public DbSet<ReadModelSnapshot> ReadModelSnapshots => Set<ReadModelSnapshot>();

    /// <summary>
    /// 注册读模型快照实体映射配置（配置位于 Leno.Infrastructure 程序集，需显式应用）。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new ReadModelSnapshotConfiguration());
    }
}
