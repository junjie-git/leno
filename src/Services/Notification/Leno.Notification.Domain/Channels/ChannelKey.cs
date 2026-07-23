using System;

namespace Leno.Notification.Domain.Channels;

/// <summary>
/// 渠道 Key 强类型字符串，替代 <see cref="global::Leno.Notification.Domain.ValueObjects.NotificationChannel"/> 枚举用于偏好存储与渠道注册表查询。
/// 使用 readonly record struct 保证值语义与高性能相等比较，避免 magic string 散落。
/// </summary>
public readonly record struct ChannelKey(string Value)
{
    /// <summary>短信渠道。</summary>
    public static readonly ChannelKey Sms = new("Sms");

    /// <summary>邮件渠道。</summary>
    public static readonly ChannelKey Email = new("Email");

    /// <summary>站内信渠道。</summary>
    public static readonly ChannelKey InApp = new("InApp");

    /// <summary>推送渠道（APP / Web Push）。</summary>
    public static readonly ChannelKey Push = new("Push");

    /// <summary>即时消息渠道（IM，如飞书 / 企微）。</summary>
    public static readonly ChannelKey IM = new("IM");

    /// <summary>Webhook 回调渠道。</summary>
    public static readonly ChannelKey Webhook = new("Webhook");

    /// <summary>空值占位，表示未设置。</summary>
    public static readonly ChannelKey Empty = new(string.Empty);

    /// <summary>隐式转换为 string，便于与现有字符串 API 交互。</summary>
    public static implicit operator string(ChannelKey key) => key.Value;

    /// <summary>从 string 隐式转换为 ChannelKey，便于构造。</summary>
    public static implicit operator ChannelKey(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判断是否为空 ChannelKey。
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
}
