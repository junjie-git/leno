using Leno.SharedKernel.Abstractions;

namespace Leno.Inventory.Domain.Events;

/// <summary>
/// 库存确认扣减领域事件，由 <see cref="Aggregates.StockReservation"/> 聚合在支付成功确认预占转真实扣减时收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Integration.Inventory.StockConfirmedIntegrationEvent"/> 集成事件对外发布。
/// </summary>
public sealed class StockConfirmedEvent : DomainEventBase
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>确认扣减数量。</summary>
    public int Quantity { get; init; }

    public StockConfirmedEvent(Guid skuId, Guid orderId, int quantity)
        : base(skuId)
    {
        SkuId = skuId;
        OrderId = orderId;
        Quantity = quantity;
    }
}
