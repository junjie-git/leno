using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Infrastructure.EventBus;

/// <summary>
/// 翻译器基类，提供按领域事件类型分发的模板方法。
/// 子类通过 <see cref="RegisterHandler{TDomain, TIntegration}"/> 注册具体翻译逻辑。
/// </summary>
public abstract class IntegrationEventMapperBase : IIntegrationEventMapper
{
    private readonly Dictionary<Type, Func<IDomainEvent, IIntegrationEvent?>> _handlers = new();

    protected void RegisterHandler<TDomain, TIntegration>(Func<TDomain, TIntegration> handler)
        where TDomain : IDomainEvent
        where TIntegration : class, IIntegrationEvent
    {
        _handlers[typeof(TDomain)] = e => handler((TDomain)e);
    }

    public IIntegrationEvent? Map(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return _handlers.TryGetValue(domainEvent.GetType(), out var handler)
            ? handler(domainEvent)
            : null;
    }
}

/// <summary>
/// 空实现，用于不需要翻译的 BC（如 Cart/Payment/Notification 无内部领域事件需对外发布）。
/// </summary>
public sealed class NullIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent) => null;
}
