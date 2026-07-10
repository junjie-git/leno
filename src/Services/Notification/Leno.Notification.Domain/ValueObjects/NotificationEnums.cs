namespace Leno.Notification.Domain.ValueObjects;

/// <summary>
/// 通知渠道。
/// </summary>
public enum NotificationChannel
{
    /// <summary>站内信。</summary>
    InApp = 0,

    /// <summary>短信。</summary>
    Sms = 1,

    /// <summary>邮件。</summary>
    Email = 2
}

/// <summary>
/// 通知发送状态。
/// </summary>
public enum NotificationStatus
{
    /// <summary>待发送。</summary>
    Pending = 0,

    /// <summary>已发送。</summary>
    Sent = 1,

    /// <summary>发送失败。</summary>
    Failed = 2,

    /// <summary>已放弃（超过最大重试次数）。</summary>
    Abandoned = 3
}

/// <summary>
/// 通知模板状态。
/// </summary>
public enum TemplateStatus
{
    /// <summary>已禁用。</summary>
    Disabled = 0,

    /// <summary>已启用。</summary>
    Enabled = 1
}

/// <summary>
/// 通知偏好状态。
/// </summary>
public enum PreferenceStatus
{
    /// <summary>已禁用（不发送任何通知）。</summary>
    Inactive = 0,

    /// <summary>已启用（按偏好发送）。</summary>
    Active = 1
}
