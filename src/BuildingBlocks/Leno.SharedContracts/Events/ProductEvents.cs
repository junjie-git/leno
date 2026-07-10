using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 商品发布集成事件，商品域发布，卖家域消费维护店铺商品数 +1。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ProductPublishedEvent : IntegrationEventBase
{
    /// <summary>商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ProductPublishedEvent() : base()
    {
    }

    public ProductPublishedEvent(Guid productId, Guid sellerId) : base()
    {
        ProductId = productId;
        SellerId = sellerId;
    }
}

/// <summary>
/// 商品下架集成事件，商品域发布，卖家域消费维护店铺商品数 -1。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ProductTakenDownEvent : IntegrationEventBase
{
    /// <summary>商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ProductTakenDownEvent() : base()
    {
    }

    public ProductTakenDownEvent(Guid productId, Guid sellerId) : base()
    {
        ProductId = productId;
        SellerId = sellerId;
    }
}
