using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Events;

/// <summary>
/// 商品发布领域事件，由 SPU 聚合在 Approve 方法中收集。
/// mapper 翻译为 ProductPublishedEvent 集成事件对外发布。
/// 消费方：卖家域（维护店铺商品数 +1）。
/// </summary>
public sealed class ProductPublishedDomainEvent : DomainEventBase
{
    /// <summary>商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    public ProductPublishedDomainEvent(Guid productId, Guid sellerId)
        : base(productId)
    {
        ProductId = productId;
        SellerId = sellerId;
    }
}
