using Leno.Points.Domain.Aggregates.PointsExchange;
using Leno.SharedKernel.Abstractions;

namespace Leno.Points.Domain.Events;

/// <summary>
/// 积分兑换完成领域事件，PointsExchange 聚合 Complete 时发布。
/// 经发件箱模式翻译为集成事件对外发布，通知业务方兑换成功。
/// </summary>
public sealed class PointsExchangeCompletedDomainEvent : DomainEventBase
{
    /// <summary>兑换记录标识。</summary>
    public Guid ExchangeId { get; init; }

    /// <summary>发起用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>兑换目标标识。</summary>
    public Guid TargetId { get; init; }

    /// <summary>消耗积分数量。</summary>
    public int PointsRequired { get; init; }

    /// <summary>兑换类型。</summary>
    public ExchangeType Type { get; init; }

    public PointsExchangeCompletedDomainEvent(Guid exchangeId, Guid userId, Guid targetId, int pointsRequired, ExchangeType type)
        : base(exchangeId)
    {
        ExchangeId = exchangeId;
        UserId = userId;
        TargetId = targetId;
        PointsRequired = pointsRequired;
        Type = type;
    }
}

/// <summary>
/// 积分兑换失败/取消领域事件，PointsExchange 聚合 Fail/Cancel 时发布。
/// 消费方据此回补积分账户余额。
/// </summary>
public sealed class PointsExchangeFailedDomainEvent : DomainEventBase
{
    /// <summary>兑换记录标识。</summary>
    public Guid ExchangeId { get; init; }

    /// <summary>发起用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>关联积分账户标识。</summary>
    public Guid PointsAccountId { get; init; }

    /// <summary>消耗积分数量（需回补）。</summary>
    public int PointsRequired { get; init; }

    /// <summary>失败/取消原因。</summary>
    public string Reason { get; init; } = string.Empty;

    public PointsExchangeFailedDomainEvent(Guid exchangeId, Guid userId, Guid pointsAccountId, int pointsRequired, string reason)
        : base(exchangeId)
    {
        ExchangeId = exchangeId;
        UserId = userId;
        PointsAccountId = pointsAccountId;
        PointsRequired = pointsRequired;
        Reason = reason ?? string.Empty;
    }
}
