namespace Leno.SharedKernel.Abstractions;

/// <summary>
/// 聚合根基类，管理领域事件集合，是聚合对外唯一入口。
/// </summary>
public abstract class AggregateRoot : Entity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    protected AggregateRoot() { }

    protected AggregateRoot(Guid id) : base(id) { }
}
