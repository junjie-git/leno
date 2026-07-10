using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Domain.Events;

/// <summary>
/// 用户密码修改成功集成事件。
/// 消费方：消息通知域（安全通知）、安全审计。
/// </summary>
public sealed class UserPasswordChangedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>密码变更时间（UTC）。</summary>
    public DateTime ChangedAt { get; init; }

    /// <summary>聚合根标识。</summary>
    public Guid AggregateId => UserId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public UserPasswordChangedEvent() : base()
    {
    }

    public UserPasswordChangedEvent(Guid userId)
        : base()
    {
        UserId = userId;
        ChangedAt = OccurredAt;
    }
}
