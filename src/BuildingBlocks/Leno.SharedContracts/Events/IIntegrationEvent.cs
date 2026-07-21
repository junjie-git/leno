namespace Leno.SharedContracts.Events;

/// <summary>
/// 集成事件契约，跨上下文传递的事件。
/// 契约定义在共享层，变更需所有消费方协商。
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>事件唯一标识，用于消费幂等去重。</summary>
    Guid EventId { get; }

    /// <summary>事件发生时间（UTC）。</summary>
    DateTime OccurredAt { get; }

    /// <summary>
    /// 幂等键，消费方据此避免重复处理（默认与 EventId 一致）。
    /// 可为 null：旧版事件 JSON 缺该字段或显式为 null 时反序列化为 null，
    /// 消费方应使用 <see cref="string.IsNullOrEmpty"/> 校验并回退到 <see cref="EventId"/> 作为幂等键。
    /// </summary>
    string? IdempotencyKey { get; }
}
