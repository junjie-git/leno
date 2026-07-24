using Leno.Infrastructure.EventBus;
using Leno.Points.Domain.Events;
using Leno.SharedContracts.Events;

namespace Leno.Points.Infrastructure.EventBus;

/// <summary>
/// Points BC 领域事件到集成事件的翻译器（积分 BC 独立维护）。
/// 将积分账户与兑换聚合收集的领域事件翻译为 SharedContracts 中的集成事件对外发布。
/// PointsReleasedDomainEvent/PointsFrozenDomainEvent/PointsConfirmedDomainEvent/PointsExpiredDomainEvent
/// 当前无跨上下文消费方，仅本上下文内消费，不在此注册翻译。
/// </summary>
public class PointsIntegrationEventMapper : IntegrationEventMapperBase
{
    public PointsIntegrationEventMapper()
    {
        // PointsEarnedDomainEvent → PointsEarnedIntegrationEvent（消息通知域积分到账通知 + Membership BC 累加成长值）
        RegisterHandler<PointsEarnedDomainEvent, PointsEarnedIntegrationEvent>(e =>
            new PointsEarnedIntegrationEvent(e.AccountId, e.UserId, e.Amount, e.Source));

        // PointsConsumedDomainEvent → PointsConsumedIntegrationEvent（消息通知域、数据分析域）
        RegisterHandler<PointsConsumedDomainEvent, PointsConsumedIntegrationEvent>(e =>
            new PointsConsumedIntegrationEvent(e.AccountId, e.UserId, e.Amount, e.ReferenceId, e.Reason));

        // PointsRevertedDomainEvent → PointsRevertedIntegrationEvent（消息通知域、数据分析域）
        RegisterHandler<PointsRevertedDomainEvent, PointsRevertedIntegrationEvent>(e =>
            new PointsRevertedIntegrationEvent(e.AccountId, e.UserId, e.Amount, e.ReferenceId, e.Reason));
    }
}
