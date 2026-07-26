using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 通知偏好响应 DTO，承载用户对各类通知事件与渠道的开关矩阵及免打扰设置。
/// </summary>
public sealed class NotificationPreferencesDto
{
    /// <summary>所属用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>事件-渠道偏好项集合。</summary>
    public IReadOnlyList<NotificationPreferenceItemDto> Preferences { get; init; } = Array.Empty<NotificationPreferenceItemDto>();

    /// <summary>是否启用免打扰时段。</summary>
    public bool DndEnabled { get; init; }

    /// <summary>免打扰起始时间（HH:mm），可空。</summary>
    public string? DndStart { get; init; }

    /// <summary>免打扰结束时间（HH:mm），可空。</summary>
    public string? DndEnd { get; init; }

    /// <summary>最后更新时间。</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 通知偏好项 DTO，对应一个事件类型的 3 个渠道开关。
/// </summary>
public sealed class NotificationPreferenceItemDto
{
    /// <summary>事件类型。</summary>
    public NotificationEventType EventType { get; init; }

    /// <summary>事件分组（订单通知/促销通知/积分通知/系统通知）。</summary>
    public string Group { get; init; } = string.Empty;

    /// <summary>事件显示名称。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>渠道开关矩阵。</summary>
    public NotificationChannelsDto Channels { get; init; } = new();
}

/// <summary>
/// 通知渠道开关 DTO。
/// </summary>
public sealed class NotificationChannelsDto
{
    /// <summary>站内信渠道是否启用（始终为 true，前端禁用关闭操作）。</summary>
    public bool InApp { get; init; }

    /// <summary>短信渠道是否启用。</summary>
    public bool Sms { get; init; }

    /// <summary>邮件渠道是否启用。</summary>
    public bool Email { get; init; }
}

/// <summary>
/// 更新通知偏好请求 DTO，支持单事件单渠道与批量更新两种模式。
/// 单事件单渠道模式（design 12-notification/preferences.md 原始契约）：<see cref="EventType"/>/<see cref="Channel"/>/<see cref="Enabled"/> 三字段；
/// 批量模式（design 13-profile/settings.md 扩展契约）：<see cref="BatchSettings"/> 非空时按矩阵全量替换。
/// 免打扰字段两种模式均可独立更新。
/// </summary>
public sealed class UpdateNotificationPreferencesRequest
{
    /// <summary>事件类型（单事件单渠道模式必填）。</summary>
    public NotificationEventType? EventType { get; init; }

    /// <summary>渠道（单事件单渠道模式必填）。</summary>
    public NotificationChannel? Channel { get; init; }

    /// <summary>开关状态（单事件单渠道模式必填）。</summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// 批量设置矩阵，非空时全量替换偏好。每项为 (EventType, Channel, Enabled) 三元组。
    /// </summary>
    public IReadOnlyList<BatchNotificationPreferenceSetting>? BatchSettings { get; init; }

    /// <summary>是否启用免打扰时段。null 表示不修改。</summary>
    public bool? DndEnabled { get; init; }

    /// <summary>免打扰起始时间（HH:mm）。仅在 <see cref="DndEnabled"/> = true 时生效。</summary>
    public string? DndStart { get; init; }

    /// <summary>免打扰结束时间（HH:mm）。仅在 <see cref="DndEnabled"/> = true 时生效。</summary>
    public string? DndEnd { get; init; }
}

/// <summary>
/// 批量通知偏好设置项。
/// </summary>
public sealed class BatchNotificationPreferenceSetting
{
    public NotificationEventType EventType { get; init; }

    public NotificationChannel Channel { get; init; }

    public bool Enabled { get; init; }
}
