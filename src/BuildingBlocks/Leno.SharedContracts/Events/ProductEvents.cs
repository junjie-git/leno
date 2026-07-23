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

    /// <summary>
    /// 受影响的 SKU 标识集合。
    /// 商品域发布时填充；购物车域消费时据此定位受影响购物车。
    /// 默认空集合，保持与旧版本发布方兼容。
    /// </summary>
    public IReadOnlyList<Guid> SkuIds { get; init; } = Array.Empty<Guid>();

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
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ProductTakenDownEvent : IntegrationEventBase
{
    /// <summary>商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>
    /// 受影响的 SKU 标识集合。
    /// 商品域发布时填充；购物车域消费时据此定位受影响购物车。
    /// 默认空集合，保持与旧版本发布方兼容。
    /// </summary>
    public IReadOnlyList<Guid> SkuIds { get; init; } = Array.Empty<Guid>();

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
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class StockAdjustedEvent : IntegrationEventBase
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
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ProductUpdatedEvent : IntegrationEventBase
{
    /// <summary>商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>商品标题（更新后）。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>主图 URL（更新后）。</summary>
    public string MainImageUrl { get; init; } = string.Empty;

    /// <summary>
    /// 受影响的 SKU 标识集合。
    /// 商品域发布时填充；购物车域消费时据此定位受影响购物车并刷新展示快照。
    /// 默认空集合，保持与旧版本发布方兼容。
    /// </summary>
    public IReadOnlyList<Guid> SkuIds { get; init; } = Array.Empty<Guid>();

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

/// <summary>
/// 商品 SKU 更新集成事件（阶段三 3.11 新增）。
/// 商品域在 SKU 价格/规格/可售状态变化时发布，消费方：购物车域（刷新本地 SKU 快照）。
/// <para>
/// 与 <see cref="ProductUpdatedEvent"/> 区别：后者仅携带商品级标题与主图（粗粒度），
/// 本事件携带 SKU 级价格/币种/规格/可售状态（细粒度），供购物车域直接更新本地快照，
/// 无需消费方再次回调商品域 ACL 查询。
/// </para>
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ProductSkuUpdatedEvent : IntegrationEventBase
{
    /// <summary>商品标识。</summary>
    public Guid ProductId { get; init; }

    /// <summary>受影响的 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>卖家（店铺）标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; init; }

    /// <summary>商品标题（更新后，用于购物车展示）。</summary>
    public string SkuName { get; init; } = string.Empty;

    /// <summary>SKU 单价（更新后）。</summary>
    public decimal Price { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>主图 URL（更新后）。</summary>
    public string? MainImageUrl { get; init; }

    /// <summary>规格文本（更新后，如"红色 / XL"）。</summary>
    public string? SpecText { get; init; }

    /// <summary>是否可售（在售且有库存）。</summary>
    public bool Available { get; init; }

    /// <summary>变更时间（UTC，由商品域填充）。</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>聚合根标识，用于发件箱归类（以 SkuId 作为聚合标识）。</summary>
    public Guid AggregateId => SkuId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ProductSkuUpdatedEvent() : base()
    {
    }

    public ProductSkuUpdatedEvent(
        Guid productId,
        Guid skuId,
        Guid sellerId,
        string skuName,
        decimal price,
        string currency,
        string? mainImageUrl,
        string? specText,
        bool available,
        DateTime updatedAt) : base()
    {
        ProductId = productId;
        SkuId = skuId;
        SellerId = sellerId;
        SkuName = skuName ?? string.Empty;
        Price = price;
        Currency = string.IsNullOrEmpty(currency) ? "CNY" : currency;
        MainImageUrl = mainImageUrl;
        SpecText = specText;
        Available = available;
        UpdatedAt = updatedAt;
    }
}

