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
/// 通知发送状态（6 状态机）。
/// 迁移前旧值：Sent=1, Abandoned=3（已移除，由 Succeeded 和 DeadLettered 替代）。
/// </summary>
public enum NotificationStatus
{
    /// <summary>待发送（初始态）。</summary>
    Pending = 0,

    /// <summary>发送中。</summary>
    Sending = 1,

    /// <summary>发送成功（终态）。</summary>
    Succeeded = 2,

    /// <summary>发送失败。</summary>
    Failed = 3,

    /// <summary>已重试。</summary>
    Retried = 4,

    /// <summary>死信/已放弃（超过最大重试次数，终态）。</summary>
    DeadLettered = 5
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
