using Leno.Infrastructure.EventBus;
using Leno.Review.Domain.Events;
using Leno.SharedContracts.Events;

namespace Leno.Review.Infrastructure.EventBus;

/// <summary>
/// Review BC 领域事件到集成事件的翻译器（评价 BC 独立维护）。
/// 将 Review 聚合收集的领域事件翻译为 SharedContracts 中的集成事件对外发布。
/// </summary>
public class ReviewIntegrationEventMapper : IntegrationEventMapperBase
{
    public ReviewIntegrationEventMapper()
    {
        // === Review 聚合 ===

        // ReviewSubmittedDomainEvent → ReviewSubmittedEvent（商品域回写商品评分摘要 score、reviewCount、好评率）
        RegisterHandler<ReviewSubmittedDomainEvent, ReviewSubmittedEvent>(e =>
            new ReviewSubmittedEvent(
                e.ReviewId, e.UserId, e.SpuId, e.Rating, e.NewScore, e.ReviewCount));

        // ReviewApprovedDomainEvent → ReviewApprovedEvent（积分域发放评价积分、商品域重算评分摘要、消息通知域）
        RegisterHandler<ReviewApprovedDomainEvent, ReviewApprovedEvent>(e =>
            new ReviewApprovedEvent(e.ReviewId, e.UserId, e.SpuId, e.Rating));

        // ReviewHiddenDomainEvent → ReviewHiddenEvent（商品域从评分统计中移除该评价）
        RegisterHandler<ReviewHiddenDomainEvent, ReviewHiddenEvent>(e =>
            new ReviewHiddenEvent(e.ReviewId, e.SpuId, e.Rating));

        // ReviewModeratedDomainEvent → ReviewModeratedEvent（商品域重算评分摘要、消息通知域）
        RegisterHandler<ReviewModeratedDomainEvent, ReviewModeratedEvent>(e =>
            new ReviewModeratedEvent(e.ReviewId, e.Status, e.Action));
    }
}
