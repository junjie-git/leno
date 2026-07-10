using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 评价提交集成事件，评价与售后域发布，卖家域消费维护店铺平均评分。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ReviewSubmittedEvent : IntegrationEventBase
{
    /// <summary>评价标识。</summary>
    public Guid ReviewId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>评分（1-5）。</summary>
    public int Rating { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ReviewSubmittedEvent() : base()
    {
    }

    public ReviewSubmittedEvent(Guid reviewId, Guid sellerId, Guid productId, int rating) : base()
    {
        ReviewId = reviewId;
        SellerId = sellerId;
        ProductId = productId;
        Rating = rating;
    }
}
