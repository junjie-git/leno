using Leno.Infrastructure.Persistence;
using Leno.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.Inventory.Infrastructure;

/// <summary>
/// Inventory BC DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露库存预占、库存预占回滚补偿、库存基线聚合与 OutboxMessage 发件箱表的 DbSet。
/// 库存真源迁入 Inventory BC 后，本上下文持有 <see cref="StockReservation"/> 与 <see cref="StockBaseline"/>
/// 的权威数据，Order BC 与 Product BC 通过集成事件保持最终一致。
/// </summary>
public sealed class InventoryDbContext : BaseDbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    /// <summary>库存预占聚合根，从 Order BC 迁入。</summary>
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    /// <summary>库存预占回滚补偿记录聚合根（T18），从 Order BC 迁入。</summary>
    public DbSet<StockReservationCompensation> StockReservationCompensations => Set<StockReservationCompensation>();

    /// <summary>库存基线聚合根，从 Product BC 迁入（中期阶段统一真源）。</summary>
    public DbSet<StockBaseline> StockBaselines => Set<StockBaseline>();
}
