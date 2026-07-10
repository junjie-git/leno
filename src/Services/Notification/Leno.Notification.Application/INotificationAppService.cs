using Leno.Notification.Application.DTOs;

namespace Leno.Notification.Application;

/// <summary>
/// 通知查询应用服务接口。
/// </summary>
public interface INotificationAppService
{
    /// <summary>分页查询用户站内信（含未读计数）。</summary>
    Task<NotificationListResultDto> GetNotificationsAsync(Guid userId, bool? isRead, int page, int pageSize, CancellationToken ct = default);

    /// <summary>按记录标识批量标记已读。</summary>
    Task MarkAsReadAsync(Guid userId, List<Guid> recordIds, CancellationToken ct = default);

    /// <summary>全部标记已读。</summary>
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>获取未读计数。</summary>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
}
