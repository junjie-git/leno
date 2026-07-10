using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 会员权益激活集成事件，会员订阅订单支付成功激活 UserMembership 时发布。
/// 消费方：消息通知域（会员开通通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class MembershipActivatedEvent : IntegrationEventBase, IDomainEvent
{
    public Guid UserId { get; init; }

    public Guid PackageId { get; init; }

    public int Level { get; init; }

    public DateTime EndTime { get; init; }

    public Guid AggregateId => UserId;

    public MembershipActivatedEvent() : base()
    {
    }

    public MembershipActivatedEvent(Guid userId, Guid packageId, int level, DateTime endTime)
        : base()
    {
        UserId = userId;
        PackageId = packageId;
        Level = level;
        EndTime = endTime;
    }
}
