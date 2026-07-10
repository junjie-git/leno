using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 会员权益激活集成事件，用户购买套餐支付成功后由会员域发布。
/// 消费方：通知域（权益开通通知）、权益域（发放会员权益）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class MembershipActivatedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>权益所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>购买的会员套餐标识。</summary>
    public Guid PackageId { get; init; }

    /// <summary>套餐对应的会员等级编号。</summary>
    public int Level { get; init; }

    /// <summary>权益到期时间（UTC）。</summary>
    public DateTime EndTime { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => UserId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public MembershipActivatedEvent() : base()
    {
    }

    public MembershipActivatedEvent(Guid userId, Guid packageId, int level, DateTime endTime) : base()
    {
        UserId = userId;
        PackageId = packageId;
        Level = level;
        EndTime = endTime;
    }
}
