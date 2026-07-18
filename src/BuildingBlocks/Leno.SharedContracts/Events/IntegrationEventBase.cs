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

    public string IdempotencyKey { get; init; }

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
        IdempotencyKey = EventId.ToString();
    }

    protected IntegrationEventBase(Guid? eventId, DateTime? occurredAt, string? idempotencyKey, int schemaVersion = 1)
    {
        EventId = eventId ?? Guid.NewGuid();
        OccurredAt = occurredAt ?? DateTime.UtcNow;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? EventId.ToString() : idempotencyKey!;
        SchemaVersion = schemaVersion < 1 ? 1 : schemaVersion;
    }
}
