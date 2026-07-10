using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分入账集成事件，积分账户 Earn 时发布。
/// 消费方：消息通知域（积分到账通知）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class PointsEarnedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>积分账户标识。</summary>
    public Guid AccountId { get; init; }

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>入账积分数量。</summary>
    public int Amount { get; init; }

    /// <summary>积分来源（CheckIn/Consumption/Activity/Refund/Offset）。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AccountId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PointsEarnedEvent() : base()
    {
    }

    public PointsEarnedEvent(Guid accountId, Guid userId, int amount, string source)
        : base()
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        Source = source ?? string.Empty;
    }
}
