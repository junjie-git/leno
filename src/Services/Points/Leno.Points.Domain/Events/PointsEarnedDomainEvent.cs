using Leno.SharedKernel.Abstractions;

namespace Leno.Points.Domain.Events;

/// <summary>
/// 积分入账领域事件，积分账户 Earn 时发布。
/// 经发件箱模式在同一事务内持久化，由 IntegrationEventMapper 翻译为
/// <see cref="Leno.SharedContracts.Events.PointsEarnedIntegrationEvent"/> 对外发布，
/// 供 Membership BC 消费累加成长值。
/// </summary>
public sealed class PointsEarnedDomainEvent : DomainEventBase
{
    /// <summary>积分账户标识。</summary>
    public Guid AccountId { get; init; }

    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>入账积分数量。</summary>
    public int Amount { get; init; }

    /// <summary>积分来源（CheckIn/Consumption/Activity/Refund/Offset/Review/NewUser/CouponExchange/Task/MemberLevelBonus）。</summary>
    public string Source { get; init; } = string.Empty;

    public PointsEarnedDomainEvent(Guid accountId, Guid userId, int amount, string source)
        : base(accountId)
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        Source = source ?? string.Empty;
    }
}
