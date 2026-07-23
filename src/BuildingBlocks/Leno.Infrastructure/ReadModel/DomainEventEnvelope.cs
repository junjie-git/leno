namespace Leno.Infrastructure.ReadModel;

/// <summary>
/// 领域事件信封，统一封装事件溯源回放所需的元数据与负载数据。
/// 事件存储以 <see cref="EventDataJson"/>（JSON 文本）形式持久化事件负载，
/// 投影器按 <see cref="EventType"/> 反序列化为具体事件类型后再投影。
/// </summary>
public sealed class DomainEventEnvelope
{
    /// <summary>事件唯一标识。</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>聚合标识。</summary>
    public string AggregateId { get; set; } = string.Empty;

    /// <summary>聚合类型名称。</summary>
    public string AggregateType { get; set; } = string.Empty;

    /// <summary>事件类型名称（用于反序列化路由）。</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>事件负载 JSON 文本。</summary>
    public string EventDataJson { get; set; } = string.Empty;

    /// <summary>事件版本号（聚合内单调递增）。</summary>
    public long Version { get; set; }

    /// <summary>事件发生时间（UTC）。</summary>
    public DateTime OccurredAt { get; set; }
}
