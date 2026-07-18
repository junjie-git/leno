using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.Infrastructure.EventBus;

/// <summary>
/// 领域事件到集成事件的翻译器抽象。
/// 各 BC Infrastructure 层实现此接口，将聚合根收集的领域事件翻译为可发布到 MQ 的集成事件。
/// 翻译返回 null 表示该领域事件无需对外发布（仅内部领域事件）。
/// </summary>
public interface IIntegrationEventMapper
{
    /// <summary>
    /// 将领域事件翻译为集成事件。
    /// </summary>
    /// <param name="domainEvent">聚合根收集的领域事件</param>
    /// <returns>对应的集成事件；若无需发布返回 null</returns>
    IIntegrationEvent? Map(IDomainEvent domainEvent);
}
