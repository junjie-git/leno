using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Events;

/// <summary>
/// 库存调整领域事件，由 SPU 聚合（UpdateStock）或 StockBaseline 聚合（Replenish）收集。
/// mapper 翻译为 StockAdjustedEvent 集成事件对外发布。
/// 消费方：订单域（同步库存基线）。
/// </summary>
public sealed class StockAdjustedDomainEvent : DomainEventBase
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>所属商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>调整后可用库存量。</summary>
    public int AvailableQty { get; init; }

    /// <summary>库存变动量（正数为补货，负数为扣减）。</summary>
    public int Delta { get; init; }

    /// <summary>调整时间（UTC）。</summary>
    public DateTime AdjustedAt { get; init; }

    public StockAdjustedDomainEvent(Guid aggregateId, Guid skuId, Guid productId, int availableQty, int delta, DateTime adjustedAt)
        : base(aggregateId)
    {
        SkuId = skuId;
        ProductId = productId;
        AvailableQty = availableQty;
        Delta = delta;
        AdjustedAt = adjustedAt;
    }
}
