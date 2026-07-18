using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.Payment.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.Payment.Infrastructure;

/// <summary>
/// 支付域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露支付单、退款单聚合与 OutboxMessage 发件箱表的 DbSet。
/// </summary>
public sealed class PaymentDbContext : BaseDbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    /// <summary>支付单聚合根。</summary>
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();

    /// <summary>退款单聚合根。</summary>
    public DbSet<RefundOrder> RefundOrders => Set<RefundOrder>();

    /// <summary>对账差异聚合根。</summary>
    public DbSet<ReconciliationDiff> ReconciliationDiffs => Set<ReconciliationDiff>();

    /// <summary>支付渠道配置聚合根。</summary>
    public DbSet<PaymentChannelConfig> PaymentChannelConfigs => Set<PaymentChannelConfig>();
}
