using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分获取集成事件，签到/消费返积分/活动奖励成功后由积分域发布。
/// 消费方：通知域（积分到账通知）、报表域（积分流水统计）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class PointsEarnedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>积分账户标识。</summary>
    public Guid AccountId { get; init; }

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>获取积分数量。</summary>
    public int Amount { get; init; }

    /// <summary>积分来源（枚举名称字符串）。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => AccountId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public PointsEarnedEvent() : base()
    {
    }

    public PointsEarnedEvent(Guid accountId, Guid userId, int amount, string source) : base()
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        Source = source;
    }
}
