using Leno.SharedKernel.Abstractions;

namespace Leno.Inventory.Domain.Events;

/// <summary>
/// 库存基线调整领域事件，由 <see cref="Aggregates.StockBaseline"/> 聚合在补货时收集。
/// 用于通知订阅方库存基线已变化（如对账/审计域、未来跨上下文消费方）。
/// </summary>
public sealed class StockAdjustedDomainEvent : DomainEventBase
{
    /// <summary>库存基线聚合标识。</summary>
    public Guid BaselineId { get; init; }

    /// <summary>所属 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>所属商品（SPU）标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>调整后的可用库存。</summary>
    public int AvailableQty { get; init; }

    /// <summary>本次调整数量（补货为正）。</summary>
    public int Delta { get; init; }

    /// <summary>调整时间（UTC）。</summary>
    public DateTime AdjustedAtUtc { get; init; }

    public StockAdjustedDomainEvent(
        Guid baselineId,
        Guid skuId,
        Guid productId,
        int availableQty,
        int delta,
        DateTime adjustedAtUtc)
        : base(baselineId)
    {
        BaselineId = baselineId;
        SkuId = skuId;
        ProductId = productId;
        AvailableQty = availableQty;
        Delta = delta;
        AdjustedAtUtc = adjustedAtUtc;
    }
}
