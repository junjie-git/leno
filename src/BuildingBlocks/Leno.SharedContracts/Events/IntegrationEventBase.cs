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
    /// 事件 schema 版本，用于 M4.2 契约治理与版本兼容。
    /// 默认 "1.0"，破坏性变更递增主版本号。
    /// </summary>
    public string SchemaVersion { get; init; } = "1.0";

    protected IntegrationEventBase()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
        IdempotencyKey = EventId.ToString();
    }

    protected IntegrationEventBase(Guid? eventId, DateTime? occurredAt, string? idempotencyKey)
    {
        EventId = eventId ?? Guid.NewGuid();
        OccurredAt = occurredAt ?? DateTime.UtcNow;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? EventId.ToString() : idempotencyKey!;
    }
}
