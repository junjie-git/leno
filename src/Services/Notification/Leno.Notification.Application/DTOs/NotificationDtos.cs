using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Application.DTOs;

/// <summary>
/// 通知记录 DTO（站内信）。
/// </summary>
public sealed class NotificationRecordDto
{
    public Guid RecordId { get; set; }
    public Guid UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    public bool IsRead { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 站内信分页查询结果。
/// </summary>
public sealed class NotificationListResultDto
{
    public List<NotificationRecordDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int UnreadCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 通知模板 DTO。
/// </summary>
public sealed class NotificationTemplateDto
{
    public Guid TemplateId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string TitleTemplate { get; set; } = string.Empty;
    public string ContentTemplate { get; set; } = string.Empty;
    public List<string> Variables { get; set; } = [];
    public TemplateStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 创建/更新通知模板请求 DTO。
/// </summary>
public sealed class SaveNotificationTemplateDto
{
    public string EventType { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string TitleTemplate { get; set; } = string.Empty;
    public string ContentTemplate { get; set; } = string.Empty;
    public List<string> Variables { get; set; } = [];
}

/// <summary>
/// 模板预览请求 DTO。
/// </summary>
public sealed class PreviewTemplateDto
{
    public Dictionary<string, string> Variables { get; set; } = [];
}

/// <summary>
/// 模板预览结果 DTO。
/// </summary>
public sealed class TemplatePreviewResultDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 通知模板分页查询结果。
/// </summary>
public sealed class NotificationTemplateListResultDto
{
    public List<NotificationTemplateDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 用户通知偏好 DTO。
/// </summary>
public sealed class NotificationPreferenceDto
{
    public Guid PreferenceId { get; set; }
    public Guid UserId { get; set; }
    public Dictionary<string, List<NotificationChannel>> EventChannels { get; set; } = [];
    public PreferenceStatus Status { get; set; }
}

/// <summary>
/// 设置渠道偏好请求 DTO。
/// </summary>
public sealed class SetChannelPreferenceDto
{
    public string EventType { get; set; } = string.Empty;
    public List<NotificationChannel> Channels { get; set; } = [];
}

/// <summary>
/// 批量标记已读请求 DTO。
/// </summary>
public sealed class MarkAsReadDto
{
    public List<Guid> RecordIds { get; set; } = [];
}
