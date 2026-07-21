using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Events;
using Leno.SharedContracts.Events;
using DomainMemberLevelUpgradedEvent = Leno.PointsMembership.Domain.Events.MemberLevelUpgradedEvent;

namespace Leno.PointsMembership.Infrastructure.EventBus;

/// <summary>
/// PointsMembership BC 领域事件到集成事件的翻译器。
/// 将积分账户与会员聚合收集的领域事件翻译为 SharedContracts 中的集成事件对外发布。
/// PointsReleasedEvent/PointsFrozenEvent/PointsExpiredEvent/PointsConfirmedEvent 当前无跨上下文消费方，仅本上下文内消费，不在此注册翻译。
/// 注意：Leno.PointsMembership.Domain.Events.MemberLevelUpgradedEvent 与 Leno.SharedContracts.Events.MemberLevelUpgradedEvent
/// 同名（前者为领域事件、后者为读模型同步专用集成事件）。本翻译器引用领域事件版本，故使用别名 DomainMemberLevelUpgradedEvent 消歧。
/// </summary>
public class PointsMembershipIntegrationEventMapper : IntegrationEventMapperBase
{
    public PointsMembershipIntegrationEventMapper()
    {
        // PointsEarnedEvent → PointsEarnedIntegrationEvent（消息通知域积分到账通知）
        RegisterHandler<PointsEarnedEvent, PointsEarnedIntegrationEvent>(e =>
            new PointsEarnedIntegrationEvent(e.AccountId, e.UserId, e.Amount, e.Source));

        // PointsConsumedEvent → PointsConsumedIntegrationEvent（消息通知域、数据分析域）
        RegisterHandler<PointsConsumedEvent, PointsConsumedIntegrationEvent>(e =>
            new PointsConsumedIntegrationEvent(e.AccountId, e.UserId, e.Amount, e.ReferenceId, e.Reason));

        // PointsRevertedEvent → PointsRevertedIntegrationEvent（消息通知域、数据分析域）
        RegisterHandler<PointsRevertedEvent, PointsRevertedIntegrationEvent>(e =>
            new PointsRevertedIntegrationEvent(e.AccountId, e.UserId, e.Amount, e.ReferenceId, e.Reason));

        // MemberLevelChangedEvent → MemberLevelChangedIntegrationEvent（消息通知域等级变更通知）
        RegisterHandler<MemberLevelChangedEvent, MemberLevelChangedIntegrationEvent>(e =>
            new MemberLevelChangedIntegrationEvent(e.UserId, e.OldLevel, e.NewLevel, e.GrowthValue));

        // DomainMemberLevelUpgradedEvent → MemberLevelChangedIntegrationEvent（消息通知域等级升级通知，复用统一等级变更集成事件）
        RegisterHandler<DomainMemberLevelUpgradedEvent, MemberLevelChangedIntegrationEvent>(e =>
            new MemberLevelChangedIntegrationEvent(e.UserId, e.OldLevel, e.NewLevel, 0));

        // MembershipActivatedEvent → PaidMemberSubscribedIntegrationEvent（消息通知域会员开通通知）
        RegisterHandler<MembershipActivatedEvent, PaidMemberSubscribedIntegrationEvent>(e =>
            new PaidMemberSubscribedIntegrationEvent(e.UserId, e.PackageId, e.Level, e.EndTime));

        // PointsExchangeCouponRequestedDomainEvent → PointsExchangeCouponRequestedEvent（优惠券域）
        // 由聚合根 RequestExchangeCoupon 内追加，经 Outbox 同事务发布给优惠券域创建优惠券
        RegisterHandler<PointsExchangeCouponRequestedDomainEvent, PointsExchangeCouponRequestedEvent>(e =>
            new PointsExchangeCouponRequestedEvent(e.ExchangeId, e.UserId, e.CouponTemplateId, e.PointsRequired));
    }
}
