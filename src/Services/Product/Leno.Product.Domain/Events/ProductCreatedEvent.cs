using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Events;

/// <summary>
/// 商品创建本地领域事件，卖家创建草稿商品时由 SPU 聚合附加。
/// 非跨上下文事件，仅在本上下文内消费（如读模型同步预留）。
/// </summary>
public sealed class ProductCreatedEvent : DomainEventBase
{
    /// <summary>商品标识。</summary>
    public Guid ProductId { get; }

    /// <summary>卖家（店铺）标识。</summary>
    public Guid SellerId { get; }

    /// <summary>商品标题。</summary>
    public string Title { get; }

    public ProductCreatedEvent(Guid productId, Guid sellerId, string title)
        : base(productId)
    {
        ProductId = productId;
        SellerId = sellerId;
        Title = title;
    }
}
