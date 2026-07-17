using Leno.SharedKernel.Abstractions;

namespace Leno.Cart.Domain.Events;

/// <summary>
/// 领域事件：SKU 已加入购物车。
/// 由 Cart 聚合 AddItem 发布，基础设施层监听以维护购物车-SKU 反向索引（Redis Set）。
/// 不实现 IIntegrationEvent，仅在上下文内部处理，不跨域发布。
/// </summary>
public sealed class SkuAddedToCartEvent : DomainEventBase
{
    /// <summary>购物车标识。</summary>
    public Guid CartId { get; }

    /// <summary>加入的 SKU 标识。</summary>
    public Guid SkuId { get; }

    public SkuAddedToCartEvent(Guid cartId, Guid skuId) : base(cartId)
    {
        CartId = cartId;
        SkuId = skuId;
    }
}

/// <summary>
/// 领域事件：SKU 已从购物车移除。
/// 由 Cart 聚合 RemoveItem 发布，基础设施层监听以维护购物车-SKU 反向索引（Redis Set）。
/// 不实现 IIntegrationEvent，仅在上下文内部处理，不跨域发布。
/// </summary>
public sealed class SkuRemovedFromCartEvent : DomainEventBase
{
    /// <summary>购物车标识。</summary>
    public Guid CartId { get; }

    /// <summary>移除的 SKU 标识。</summary>
    public Guid SkuId { get; }

    public SkuRemovedFromCartEvent(Guid cartId, Guid skuId) : base(cartId)
    {
        CartId = cartId;
        SkuId = skuId;
    }
}
