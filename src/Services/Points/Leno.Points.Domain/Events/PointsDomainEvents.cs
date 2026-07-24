using Leno.SharedKernel.Abstractions;

namespace Leno.Points.Domain.Events;

/// <summary>
/// 积分冻结领域事件，积分账户 Freeze 时发布。
/// 当前为上下文内部领域事件，无跨上下文消费方。
/// </summary>
public sealed class PointsFrozenDomainEvent : DomainEventBase
{
    public Guid AccountId { get; init; }
    public Guid UserId { get; init; }
    public int Amount { get; init; }
    public Guid OrderId { get; init; }

    public PointsFrozenDomainEvent(Guid accountId, Guid userId, int amount, Guid orderId)
        : base(accountId)
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        OrderId = orderId;
    }
}

/// <summary>
/// 积分确认扣减领域事件，支付成功确认冻结积分时发布。
/// 上下文内部领域事件，当前无跨上下文消费方。
/// </summary>
public sealed class PointsConfirmedDomainEvent : DomainEventBase
{
    public Guid AccountId { get; init; }
    public Guid UserId { get; init; }
    public int Amount { get; init; }
    public Guid OrderId { get; init; }

    public PointsConfirmedDomainEvent(Guid accountId, Guid userId, int amount, Guid orderId)
        : base(accountId)
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        OrderId = orderId;
    }
}

/// <summary>
/// 积分释放领域事件，订单取消回退冻结积分时发布。
/// 上下文内部领域事件，当前无跨上下文消费方。
/// </summary>
public sealed class PointsReleasedDomainEvent : DomainEventBase
{
    public Guid AccountId { get; init; }
    public Guid UserId { get; init; }
    public int Amount { get; init; }
    public Guid OrderId { get; init; }

    public PointsReleasedDomainEvent(Guid accountId, Guid userId, int amount, Guid orderId)
        : base(accountId)
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        OrderId = orderId;
    }
}

/// <summary>
/// 积分直接消费领域事件，积分账户 ConsumePoints 时发布。
/// </summary>
public sealed class PointsConsumedDomainEvent : DomainEventBase
{
    public Guid AccountId { get; init; }
    public Guid UserId { get; init; }
    public int Amount { get; init; }
    public Guid ReferenceId { get; init; }
    public string Reason { get; init; } = string.Empty;

    public PointsConsumedDomainEvent(Guid accountId, Guid userId, int amount, Guid referenceId, string reason)
        : base(accountId)
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        ReferenceId = referenceId;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>
/// 积分扣回领域事件，退款/售后扣回已发放积分时发布。
/// </summary>
public sealed class PointsRevertedDomainEvent : DomainEventBase
{
    public Guid AccountId { get; init; }
    public Guid UserId { get; init; }
    public int Amount { get; init; }
    public Guid ReferenceId { get; init; }
    public string Reason { get; init; } = string.Empty;

    public PointsRevertedDomainEvent(Guid accountId, Guid userId, int amount, Guid referenceId, string reason)
        : base(accountId)
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        ReferenceId = referenceId;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>
/// 积分过期清理领域事件，积分账户 ExpirePoints 时发布。
/// </summary>
public sealed class PointsExpiredDomainEvent : DomainEventBase
{
    public Guid UserId { get; init; }
    public int Points { get; init; }
    public DateTime ExpiredAt { get; init; }

    public PointsExpiredDomainEvent(Guid userId, int points, DateTime expiredAt)
        : base(userId)
    {
        UserId = userId;
        Points = points;
        ExpiredAt = expiredAt;
    }
}
