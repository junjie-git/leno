using Leno.SharedKernel.Abstractions;
using NotificationPreferenceAggregate = Leno.Notification.Domain.Aggregates.NotificationPreference;

namespace Leno.Notification.Domain.Repositories;

/// <summary>
/// 通知偏好仓储接口。
/// </summary>
public interface INotificationPreferenceRepository : IRepository<NotificationPreferenceAggregate>
{
    /// <summary>按用户查询偏好，不存在返回 null。</summary>
    Task<NotificationPreferenceAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
