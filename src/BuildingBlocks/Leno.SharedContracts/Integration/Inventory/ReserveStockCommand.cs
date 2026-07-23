using Leno.SharedContracts.Events;
using MassTransit;

namespace Leno.SharedContracts.Integration.Inventory;

/// <summary>
/// 库存预占命令（Order BC → Inventory BC）。
/// 由 Order Saga 编排器发布，Inventory BC 的 <c>ReserveStockCommandConsumer</c> 消费后调用 <c>IInventoryAppService.ReserveAsync</c>。
/// </summary>
public sealed record ReserveStockCommand(
    Guid OrderId,
    IReadOnlyList<ReserveStockItem> Items,
    Guid IdempotencyKey,
    TimeSpan? ReservationTtl = null) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致，供 Saga 状态机路由。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 库存预占命令明细项。
/// </summary>
public sealed record ReserveStockItem(Guid SkuId, int Quantity, long SellerId);
