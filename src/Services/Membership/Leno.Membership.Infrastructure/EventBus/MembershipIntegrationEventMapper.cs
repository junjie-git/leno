using Leno.Infrastructure.EventBus;
using Leno.Membership.Domain.Events;
using Leno.SharedContracts.Events;

namespace Leno.Membership.Infrastructure.EventBus;

/// <summary>
/// Membership BC 领域事件到集成事件的翻译器（会员 BC 独立维护）。
/// 将会员聚合收集的领域事件翻译为 SharedContracts 中的集成事件对外发布。
/// </summary>
public class MembershipIntegrationEventMapper : IntegrationEventMapperBase
{
    public MembershipIntegrationEventMapper()
    {
        // MemberLevelChangedDomainEvent → MemberLevelChangedIntegrationEvent
        // 消费方：Points BC 发放等级提升奖励积分、消息通知域发送等级变更通知
        RegisterHandler<MemberLevelChangedDomainEvent, MemberLevelChangedIntegrationEvent>(e =>
            new MemberLevelChangedIntegrationEvent(e.UserId, e.OldLevel, e.NewLevel, e.GrowthValue));
    }
}
