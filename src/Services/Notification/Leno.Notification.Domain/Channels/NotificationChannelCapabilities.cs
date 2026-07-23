using System;

namespace Leno.Notification.Domain.Channels;

/// <summary>
/// 通知渠道能力声明，描述渠道在限流 / 重试 / 回执 / 模板 / 超时等维度的能力。
/// 由 <see cref="INotificationChannel"/> 实现自描述，驱动调度器 / 限流器 / 重试策略选择行为。
/// </summary>
/// <param name="RequiresRateLimit">是否需要频率限制（短信为 true，站内信为 false）。</param>
/// <param name="SupportsAsyncReceipt">是否支持异步回执（短信 / 邮件为 true，站内信为 false）。</param>
/// <param name="IsIdempotent">是否天然幂等（站内信写入即送达，重复发送无副作用，为 true）。</param>
/// <param name="SupportsTemplate">是否支持模板渲染（所有现有渠道均支持，为 true）。</param>
/// <param name="Timeout">单次发送的超时时间，null 表示无超时（站内信即时完成）。</param>
public sealed record NotificationChannelCapabilities(
    bool RequiresRateLimit,
    bool SupportsAsyncReceipt,
    bool IsIdempotent,
    bool SupportsTemplate,
    TimeSpan? Timeout);
