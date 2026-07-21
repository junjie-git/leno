using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Events;

/// <summary>
/// 订单软删除领域事件（P1-T26），由 <see cref="Aggregates.Order.SoftDelete"/> 方法收集。
/// 标识订单已被标记为 <c>IsDeleted=true</c>，默认查询过滤器自动排除。
/// 可由 mapper 翻译为集成事件通知下游域（如搜索索引移除、推荐列表排除）。
/// </summary>
public sealed class OrderSoftDeletedDomainEvent : DomainEventBase
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>操作人标识，可为空（系统自动操作）。</summary>
    public Guid OperatorId { get; init; }

    /// <summary>软删除时间（UTC）。</summary>
    public DateTime DeletedAt { get; init; }

    public OrderSoftDeletedDomainEvent(Guid orderId, Guid operatorId, DateTime deletedAt)
        : base(orderId)
    {
        OrderId = orderId;
        OperatorId = operatorId;
        DeletedAt = deletedAt;
    }
}
