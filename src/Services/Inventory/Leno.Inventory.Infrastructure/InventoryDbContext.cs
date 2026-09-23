using Leno.Infrastructure.Persistence;
using Leno.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.Inventory.Infrastructure;

/// <summary>
/// Inventory BC DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露库存台账（订单 × SKU 占用记录）与库存基线（SKU 计数器）两个集合。
/// 库存真源迁入 Inventory BC 后（双轨下线 DEC-4，2026-09-22），本上下文是库存数量的唯一权威存储；
/// 权威存储为 SQL Server 单库 —— 台账与基线在同一事务内更新，不再依赖 Redis 权威 + 对账兜底。
/// </summary>
public sealed class InventoryDbContext : BaseDbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    /// <summary>库存台账（订单 × SKU 维度占用记录，幂等与审计的唯一事实来源）。</summary>
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    /// <summary>库存基线（SKU 维度计数器：可用 / 预占 / 已扣减），从 Product BC 迁入。</summary>
    public DbSet<StockBaseline> StockBaselines => Set<StockBaseline>();
}
