namespace Leno.UserCenter.Domain.ValueObjects;

/// <summary>
/// 通知渠道枚举，对应通知偏好的渠道开关矩阵。
/// 站内信（InApp）默认开启且不可关闭，保证关键通知触达。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// </summary>
public enum NotificationChannel
{
    /// <summary>站内信渠道（默认开启且不可关闭）。</summary>
    InApp = 1,

    /// <summary>短信渠道。</summary>
    Sms = 2,

    /// <summary>邮件渠道。</summary>
    Email = 3
}
