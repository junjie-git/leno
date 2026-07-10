using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分释放集成事件，订单取消回退冻结积分后由积分域发布。
/// 消费方：订单域（解除冻结记录）、通知域（积分回补通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class PointsReleasedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>积分账户标识。</summary>
    public Guid AccountId { get; init; }

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>释放回退积分数量。</summary>
    public int Amount { get; init; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AccountId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PointsReleasedEvent() : base()
    {
    }

    public PointsReleasedEvent(Guid accountId, Guid userId, int amount, Guid orderId) : base()
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        OrderId = orderId;
    }
}
