using System.Text.Json.Serialization;

namespace Leno.SharedKernel.Abstractions;

/// <summary>
/// 聚合根基类，管理领域事件集合，是聚合对外唯一入口。
/// </summary>
public abstract class AggregateRoot : Entity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// 聚合已发布的领域事件集合（只读视图）。
    /// <para>
    /// P1-1：标注 <see cref="JsonIgnoreAttribute"/> 避免领域事件被序列化到 Redis JSON 等持久化存储。
    /// 领域事件是瞬态通知，不属于聚合持久化状态，即使 <c>ClearDomainEvents</c> 后空数组也不应写入。
    /// </para>
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    protected AggregateRoot() { }

    protected AggregateRoot(Guid id) : base(id) { }
}
