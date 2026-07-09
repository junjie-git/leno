namespace Leno.SharedKernel.Abstractions;

/// <summary>
/// 持有领域事件集合的契约，由工作单元在事务提交后派发。
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
