using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Application;

/// <summary>
/// 频率限制管理应用服务接口（运营端）。
/// </summary>
public interface IRateLimitAppService
{
    /// <summary>获取指定渠道的频率限制配置。</summary>
    Task<RateLimitConfigDto> GetRateLimitAsync(NotificationChannel channel, CancellationToken ct = default);

    /// <summary>更新指定渠道的频率限制配置。</summary>
    Task UpdateRateLimitAsync(Guid operatorId, NotificationChannel channel, SaveRateLimitConfigDto dto, CancellationToken ct = default);
}