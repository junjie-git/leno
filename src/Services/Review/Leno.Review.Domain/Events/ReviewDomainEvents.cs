using Leno.SharedKernel.Abstractions;

namespace Leno.Review.Domain.Events;

/// <summary>
/// 评价提交领域事件，由 <see cref="Aggregates.Review"/> 聚合在 Create 工厂方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ReviewSubmittedEvent"/> 集成事件对外发布。
/// 消费方：商品域（回写商品评分摘要 score、reviewCount、好评率）。
/// </summary>
public sealed class ReviewSubmittedDomainEvent : DomainEventBase
{
    public Guid ReviewId { get; init; }
    public Guid UserId { get; init; }
    public Guid SpuId { get; init; }
    public int Rating { get; init; }
    public double NewScore { get; init; }
    public int ReviewCount { get; init; }

    public ReviewSubmittedDomainEvent(
        Guid reviewId, Guid userId, Guid spuId, int rating, double newScore, int reviewCount)
        : base(reviewId)
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        Rating = rating;
        NewScore = newScore;
        ReviewCount = reviewCount;
    }
}

/// <summary>
/// 评价审核通过领域事件，由 <see cref="Aggregates.Review"/> 聚合在 Approve 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ReviewApprovedEvent"/> 集成事件对外发布。
/// 消费方：积分域（驱动积分发放）、商品域（重算评分摘要）、消息通知域。
/// </summary>
public sealed class ReviewApprovedDomainEvent : DomainEventBase
{
    public Guid ReviewId { get; init; }
    public Guid UserId { get; init; }
    public Guid SpuId { get; init; }
    public int Rating { get; init; }

    public ReviewApprovedDomainEvent(Guid reviewId, Guid userId, Guid spuId, int rating)
        : base(reviewId)
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        Rating = rating;
    }
}

/// <summary>
/// 评价隐藏领域事件，由 <see cref="Aggregates.Review"/> 聚合在 Hide 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ReviewHiddenEvent"/> 集成事件对外发布。
/// 消费方：商品域（重算评分摘要，隐藏后从统计中移除）。
/// </summary>
public sealed class ReviewHiddenDomainEvent : DomainEventBase
{
    public Guid ReviewId { get; init; }
    public Guid SpuId { get; init; }
    public int Rating { get; init; }

    public ReviewHiddenDomainEvent(Guid reviewId, Guid spuId, int rating)
        : base(reviewId)
    {
        ReviewId = reviewId;
        SpuId = spuId;
        Rating = rating;
    }
}

/// <summary>
/// 评价审核结果领域事件，表达运营审核（通过/隐藏）后的结果事实。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ReviewModeratedEvent"/> 集成事件对外发布。
/// 消费方：商品域（重算评分摘要）、消息通知域。
/// Status 为 int 而非枚举，与集成事件契约保持一致；发布方按 (int)ReviewStatus 转换。
/// </summary>
public sealed class ReviewModeratedDomainEvent : DomainEventBase
{
    public Guid ReviewId { get; init; }
    public int Status { get; init; }
    public string Action { get; init; } = string.Empty;

    public ReviewModeratedDomainEvent(Guid reviewId, int status, string action)
        : base(reviewId)
    {
        ReviewId = reviewId;
        Status = status;
        Action = action ?? string.Empty;
    }
}
