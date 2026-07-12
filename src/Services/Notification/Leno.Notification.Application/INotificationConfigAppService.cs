using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Application;

/// <summary>
/// 通知渠道配置管理应用服务接口（运营端）。
/// </summary>
public interface INotificationConfigAppService
{
    /// <summary>获取指定渠道的配置（敏感字段脱敏显示）。</summary>
    Task<NotificationConfigDto> GetConfigAsync(NotificationChannel channel, CancellationToken ct = default);

    /// <summary>更新指定渠道的配置。</summary>
    Task UpdateConfigAsync(Guid operatorId, NotificationChannel channel, SaveNotificationConfigDto dto, CancellationToken ct = default);

    /// <summary>测试发送验证渠道配置是否正确。</summary>
    Task<TestSendResultDto> TestSendAsync(NotificationChannel channel, TestSendRequestDto dto, CancellationToken ct = default);
}