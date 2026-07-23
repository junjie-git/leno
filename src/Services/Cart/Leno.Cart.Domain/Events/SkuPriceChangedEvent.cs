using Leno.Cart.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Cart.Domain.Events;

/// <summary>
/// 领域事件：购物车项 SKU 价格发生变化。
/// 由 <see cref="Leno.Cart.Domain.Aggregates.CartItem.UpdateSnapshot"/> 在快照刷新后价格变动时发布，
/// 供基础设施层记录价格变更审计或触发下游联动（如结算预览失效）。
/// 不实现 IIntegrationEvent，仅在上下文内部处理，不跨域发布。
/// </summary>
public sealed class SkuPriceChangedEvent : DomainEventBase
{
    /// <summary>购物车标识。</summary>
    public Guid CartId { get; }

    /// <summary>购物车项标识。</summary>
    public Guid CartItemId { get; }

    /// <summary>商品 SKU 标识。</summary>
    public Guid SkuId { get; }

    /// <summary>变更前价格。</summary>
    public decimal OldPrice { get; }

    /// <summary>变更后价格。</summary>
    public decimal NewPrice { get; }

    /// <summary>变更后币种。</summary>
    public string Currency { get; }

    public SkuPriceChangedEvent(
        Guid cartId,
        Guid cartItemId,
        Guid skuId,
        decimal oldPrice,
        decimal newPrice,
        string currency)
        : base(cartId)
    {
        CartId = cartId;
        CartItemId = cartItemId;
        SkuId = skuId;
        OldPrice = oldPrice;
        NewPrice = newPrice;
        Currency = currency ?? "CNY";
    }
}
