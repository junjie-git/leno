using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

/// <summary>
/// 积分释放领域事件，订单取消释放冻结积分时发布。
/// 上下文内部领域事件，当前无跨上下文消费方，不翻译为集成事件。
/// </summary>
public sealed class PointsReleasedEvent : DomainEventBase
{
    public Guid AccountId { get; init; }

    public Guid UserId { get; init; }

    public int Amount { get; init; }

    public Guid OrderId { get; init; }

    public PointsReleasedEvent(Guid accountId, Guid userId, int amount, Guid orderId)
        : base(accountId)
    {
        AccountId = accountId;
        UserId = userId;
        Amount = amount;
        OrderId = orderId;
    }
}
