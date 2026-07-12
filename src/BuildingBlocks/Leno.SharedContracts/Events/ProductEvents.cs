using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 商品发布集成事件，商品域发布，卖家域消费维护店铺商品数 +1。
/// 同时实现 <see cref="IDomainEvent"/> 以便商品域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ProductPublishedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ProductId;

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
/// 同时实现 <see cref="IDomainEvent"/> 以便商品域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ProductTakenDownEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ProductId;

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

/// <summary>
/// 库存调整集成事件，商品域发布。
/// 消费方：订单域（同步库存基线）。
/// 同时实现 <see cref="IDomainEvent"/> 以便商品域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class StockAdjustedEvent : IntegrationEventBase, IDomainEvent
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

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => SkuId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public StockAdjustedEvent() : base()
    {
    }

    public StockAdjustedEvent(Guid skuId, Guid productId, int availableQty, int delta, DateTime adjustedAt) : base()
    {
        SkuId = skuId;
        ProductId = productId;
        AvailableQty = availableQty;
        Delta = delta;
        AdjustedAt = adjustedAt;
    }
}

/// <summary>
/// 商品更新集成事件，商品域在商品信息变更时发布。
/// 消费方：购物车域（刷新展示快照）、搜索域（同步 ES 读模型）。
/// 同时实现 <see cref="IDomainEvent"/> 以便商品域经发件箱模式在同一事务内持久化。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ProductUpdatedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>商品标题（更新后）。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>主图 URL（更新后）。</summary>
    public string MainImageUrl { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ProductId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ProductUpdatedEvent() : base()
    {
    }

    public ProductUpdatedEvent(Guid productId, Guid sellerId, string title, string mainImageUrl) : base()
    {
        ProductId = productId;
        SellerId = sellerId;
        Title = title ?? string.Empty;
        MainImageUrl = mainImageUrl ?? string.Empty;
    }
}

