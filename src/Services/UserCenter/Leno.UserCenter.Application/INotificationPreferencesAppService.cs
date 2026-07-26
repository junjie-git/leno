using Leno.UserCenter.Application.DTOs;

namespace Leno.UserCenter.Application;

/// <summary>
/// 通知偏好应用服务，编排查询与更新用户通知偏好用例。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public interface INotificationPreferencesAppService
{
    /// <summary>查询当前用户通知偏好。若用户首次访问，懒初始化为默认偏好并持久化。</summary>
    Task<NotificationPreferencesDto> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 更新通知偏好。支持单事件单渠道与批量矩阵两种模式：
    /// 当 <see cref="UpdateNotificationPreferencesRequest.BatchSettings"/> 非空时全量替换偏好矩阵；
    /// 否则按 <see cref="UpdateNotificationPreferencesRequest.EventType"/>/<see cref="UpdateNotificationPreferencesRequest.Channel"/>/<see cref="UpdateNotificationPreferencesRequest.Enabled"/> 更新单点。
    /// 免打扰字段独立处理，仅在 <see cref="UpdateNotificationPreferencesRequest.DndEnabled"/> 非 null 时更新。
    /// </summary>
    Task<NotificationPreferencesDto> UpdateAsync(
        Guid userId,
        UpdateNotificationPreferencesRequest request,
        CancellationToken ct = default);
}
