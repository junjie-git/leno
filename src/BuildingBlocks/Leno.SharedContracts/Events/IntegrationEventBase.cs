namespace Leno.SharedContracts.Events;

/// <summary>
/// 集成事件抽象基类，统一生成 EventId、OccurredAt、IdempotencyKey。
/// 跨上下文集成事件记录推荐继承此类。
/// 仅实现 <see cref="IIntegrationEvent"/>，不含领域事件语义。
/// </summary>
public abstract class IntegrationEventBase : IIntegrationEvent
{
    public Guid EventId { get; init; }

    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// 幂等键，用于消费者去重。
    /// 类型为 <c>string?</c>（可空）：旧版事件 JSON 显式为 null 或反序列化器配置忽略默认值时可为 null。
    /// 默认 <c>string.Empty</c> 保持向后兼容（旧版 JSON 缺该字段时反序列化为空字符串而非 null）。
    /// 消费侧应使用 <see cref="string.IsNullOrEmpty"/> 校验并回退到 <see cref="EventId"/> 作为幂等键。
    /// </summary>
    public string? IdempotencyKey { get; init; } = string.Empty;

    /// <summary>
    /// 事件模式版本号（M4.2）。
    /// 默认 1，事件字段变更时递增；消费者可按 SchemaVersion 路由不同 handler。
    /// Outbox 持久化此字段，跨 BC 消费方据此判断是否需升级反序列化逻辑。
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    protected IntegrationEventBase()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
        // IdempotencyKey 由字段初始化器默认为 string.Empty。
        // System.Text.Json 反序列化旧版事件缺该字段时保持 string.Empty（字段初始化器不被覆盖）；
        // 若 JSON 显式为 null，则反序列化为 null（类型为 string? 允许 null）。
        // 消费侧通过 string.IsNullOrEmpty 校验后回退到 EventId 作为幂等键（参见 IntegrationEventConsumerBase）。
    }

    protected IntegrationEventBase(Guid? eventId, DateTime? occurredAt, string? idempotencyKey, int schemaVersion = 1)
    {
        EventId = eventId ?? Guid.NewGuid();
        OccurredAt = occurredAt ?? DateTime.UtcNow;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? EventId.ToString() : idempotencyKey;
        SchemaVersion = schemaVersion < 1 ? 1 : schemaVersion;
    }
}
