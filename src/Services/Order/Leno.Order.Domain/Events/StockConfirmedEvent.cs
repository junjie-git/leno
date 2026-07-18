using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Events;

/// <summary>
/// 库存确认扣减领域事件，订单域在支付成功后确认预占转真实扣减时由 <see cref="Aggregates.StockReservation"/> 聚合收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.StockConfirmedIntegrationEvent"/> 集成事件对外发布（若需跨上下文消费）。
/// 当前无跨上下文消费方，事件仅在本上下文内消费。
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
