namespace Leno.SharedContracts.Events;

/// <summary>
/// 集成事件抽象基类，统一生成 EventId、OccurredAt、IdempotencyKey。
/// 跨上下文集成事件记录推荐继承此类。
/// </summary>
public abstract class IntegrationEventBase : IIntegrationEvent
{
    public Guid EventId { get; init; }

    public DateTime OccurredAt { get; init; }

    public string IdempotencyKey { get; init; }

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
