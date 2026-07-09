namespace Leno.SharedKernel.Abstractions;

/// <summary>
/// 领域事件契约，表达上下文内部已发生的重要业务事实。
/// </summary>
public interface IDomainEvent
{
    /// <summary>事件唯一标识。</summary>
    Guid EventId { get; }

    /// <summary>事件发生时间（UTC）。</summary>
    DateTime OccurredAt { get; }

    /// <summary>产生事件的聚合根标识。</summary>
    Guid AggregateId { get; }
}

/// <summary>
/// 领域事件抽象基类，统一生成 EventId 与 OccurredAt。
/// </summary>
public abstract class DomainEventBase : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTime OccurredAt { get; } = DateTime.UtcNow;

    public Guid AggregateId { get; }

    protected DomainEventBase(Guid aggregateId)
    {
        AggregateId = aggregateId;
    }
}
