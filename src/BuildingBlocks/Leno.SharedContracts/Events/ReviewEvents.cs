using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 评价提交集成事件，评价与售后域在评价提交时发布。
/// 消费方：商品域（回写商品评分摘要 score、reviewCount、好评率）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ReviewSubmittedEvent : IntegrationEventBase
{
    /// <summary>评价标识。</summary>
    public Guid ReviewId { get; init; }

    /// <summary>评价人（买家）标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>
    /// 店铺标识，由评价域在创建评价时从订单反查真实 ShopId 后填充。
    /// 默认 Guid.Empty 保持向后兼容；SellerShop BC 消费时按此字段同步工作台统计。
    /// </summary>
    public Guid ShopId { get; init; }

    /// <summary>评分（1-5）。</summary>
    public int Rating { get; init; }

    /// <summary>提交后商品的新平均评分（加权平均）。</summary>
    public double NewScore { get; init; }

    /// <summary>提交后商品的可见评价总数。</summary>
    public int ReviewCount { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ReviewId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ReviewSubmittedEvent() : base()
    {
    }

    public ReviewSubmittedEvent(Guid reviewId, Guid userId, Guid spuId, int rating) : base()
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        Rating = rating;
    }

    public ReviewSubmittedEvent(Guid reviewId, Guid userId, Guid spuId, int rating, double newScore, int reviewCount) : base()
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        Rating = rating;
        NewScore = newScore;
        ReviewCount = reviewCount;
    }

    /// <summary>带 ShopId 的构造重载，由评价域创建评价时发布。SchemaVersion 递增为 2。</summary>
    public ReviewSubmittedEvent(Guid reviewId, Guid userId, Guid spuId, Guid shopId, int rating, double newScore, int reviewCount)
        : base(eventId: null, occurredAt: null, idempotencyKey: null, schemaVersion: 2)
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        ShopId = shopId;
        Rating = rating;
        NewScore = newScore;
        ReviewCount = reviewCount;
    }
}

/// <summary>
/// 评价审核通过集成事件，评价与售后域在运营审核通过评价时发布。
/// 消费方：积分域（驱动积分发放）、商品域（重算评分摘要）、消息通知域。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ReviewApprovedEvent : IntegrationEventBase
{
    /// <summary>评价标识。</summary>
    public Guid ReviewId { get; init; }

    /// <summary>评价人（买家）标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>店铺标识，由评价域发布审核通过事件时填充。默认 Guid.Empty 保持向后兼容。</summary>
    public Guid ShopId { get; init; }

    /// <summary>评分（1-5）。</summary>
    public int Rating { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ReviewId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ReviewApprovedEvent() : base()
    {
    }

    public ReviewApprovedEvent(Guid reviewId, Guid userId, Guid spuId, int rating) : base()
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        Rating = rating;
    }

    /// <summary>带 ShopId 的构造重载。SchemaVersion 递增为 2。</summary>
    public ReviewApprovedEvent(Guid reviewId, Guid userId, Guid spuId, Guid shopId, int rating)
        : base(eventId: null, occurredAt: null, idempotencyKey: null, schemaVersion: 2)
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        ShopId = shopId;
        Rating = rating;
    }
}

/// <summary>
/// 评价隐藏集成事件，评价与售后域在运营隐藏违规评价时发布。
/// 消费方：商品域（重算评分摘要，隐藏后从统计中移除）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ReviewHiddenEvent : IntegrationEventBase
{
    /// <summary>评价标识。</summary>
    public Guid ReviewId { get; init; }

    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>店铺标识，由评价域发布隐藏事件时填充。默认 Guid.Empty 保持向后兼容。</summary>
    public Guid ShopId { get; init; }

    /// <summary>评分（1-5），用于商品域从评分统计中移除。</summary>
    public int Rating { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ReviewId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ReviewHiddenEvent() : base()
    {
    }

    public ReviewHiddenEvent(Guid reviewId, Guid spuId, int rating) : base()
    {
        ReviewId = reviewId;
        SpuId = spuId;
        Rating = rating;
    }

    /// <summary>带 ShopId 的构造重载。SchemaVersion 递增为 2。</summary>
    public ReviewHiddenEvent(Guid reviewId, Guid spuId, Guid shopId, int rating)
        : base(eventId: null, occurredAt: null, idempotencyKey: null, schemaVersion: 2)
    {
        ReviewId = reviewId;
        SpuId = spuId;
        ShopId = shopId;
        Rating = rating;
    }
}

/// <summary>
/// 评价审核结果集成事件，评价与售后域在运营审核（通过/隐藏）后发布。
/// 消费方：商品域（重算评分摘要）、消息通知域。
/// Status 为 int 而非枚举，因共享契约层不可引用领域层枚举；发布方按 (int)ReviewStatus 转换。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ReviewModeratedEvent : IntegrationEventBase
{
    /// <summary>评价标识。</summary>
    public Guid ReviewId { get; init; }

    /// <summary>审核后状态（ReviewStatus 枚举的 int 值：1=Approved, 2=Hidden）。</summary>
    public int Status { get; init; }

    /// <summary>审核动作（approve 通过 / hide 隐藏）。</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ReviewId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ReviewModeratedEvent() : base()
    {
    }

    public ReviewModeratedEvent(Guid reviewId, int status, string action) : base()
    {
        ReviewId = reviewId;
        Status = status;
        Action = action ?? string.Empty;
    }
}
