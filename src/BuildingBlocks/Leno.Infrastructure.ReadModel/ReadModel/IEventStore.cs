namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 事件存储抽象，提供按版本读取聚合事件流的能力，用于读模型增量回放。
/// 全量事件溯源实现由各 BC 的事件存储模块提供；本接口仅定义读模型重建所需的最小契约。
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// 从指定版本之后读取聚合的事件流（不含 <paramref name="fromVersion"/> 本身对应的版本）。
    /// <paramref name="fromVersion"/> 为 0 时返回聚合全部事件。
    /// 返回的事件流按 <see cref="DomainEventEnvelope.Version"/> 升序。
    /// </summary>
    /// <param name="aggregateId">聚合标识。</param>
    /// <param name="fromVersion">起始版本号（返回版本号大于此值的事件）。</param>
    /// <param name="ct">取消令牌。</param>
    IAsyncEnumerable<DomainEventEnvelope> GetEventsFromVersion(
        string aggregateId,
        long fromVersion,
        CancellationToken ct = default);
}
