using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 会员权益激活领域事件，会员订阅订单支付成功激活 UserMembership 时发布。
/// 经发件箱模式在同一事务内持久化，由 IntegrationEventMapper 翻译为 PaidMemberSubscribedIntegrationEvent 对外发布。
/// </summary>
public sealed class MembershipActivatedEvent : DomainEventBase
{
    public Guid UserId { get; init; }

    public Guid PackageId { get; init; }

    public int Level { get; init; }

    public DateTime EndTime { get; init; }

    public MembershipActivatedEvent(Guid userId, Guid packageId, int level, DateTime endTime)
        : base(userId)
    {
        UserId = userId;
        PackageId = packageId;
        Level = level;
        EndTime = endTime;
    }
}
