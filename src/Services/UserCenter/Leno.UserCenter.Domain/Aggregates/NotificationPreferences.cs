using Leno.SharedKernel.Abstractions;

namespace Leno.UserCenter.Domain.Aggregates;

/// <summary>
/// 通知偏好聚合根骨架（Task A5 占位，Task A6 从 UserAuth.Domain 迁入完整实现）。
/// 与 Notification 域共享表，参见 Spec §4.3.5。
/// </summary>
public sealed class NotificationPreferences : AggregateRoot
{
    private NotificationPreferences() { }

    private NotificationPreferences(Guid id) : base(id) { }
}
