using Leno.Notification.Application.DTOs;

namespace Leno.Notification.Application;

/// <summary>
/// 用户通知偏好管理应用服务接口。
/// </summary>
public interface INotificationPreferenceAppService
{
    /// <summary>查询当前用户通知偏好。</summary>
    Task<NotificationPreferenceDto> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>设置某事件渠道偏好。</summary>
    Task SetChannelPreferenceAsync(Guid userId, SetChannelPreferenceDto dto, CancellationToken ct = default);
}
