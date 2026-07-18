using Leno.SharedKernel.Abstractions;

namespace Leno.Product.Domain.Events;

/// <summary>
/// 商品更新领域事件，由 SPU 聚合在商品信息变更时收集。
/// mapper 翻译为 ProductUpdatedEvent 集成事件对外发布。
/// 消费方：购物车域（刷新展示快照）、搜索域（同步 ES 读模型）。
/// </summary>
public sealed class ProductUpdatedDomainEvent : DomainEventBase
{
    /// <summary>商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>商品标题（更新后）。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>主图 URL（更新后）。</summary>
    public string MainImageUrl { get; init; } = string.Empty;

    public ProductUpdatedDomainEvent(Guid productId, Guid sellerId, string title, string mainImageUrl)
        : base(productId)
    {
        ProductId = productId;
        SellerId = sellerId;
        Title = title ?? string.Empty;
        MainImageUrl = mainImageUrl ?? string.Empty;
    }
}
