using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 通知发送渠道接口，各渠道实现具体发送逻辑。
/// </summary>
public interface IChannel
{
    /// <summary>渠道类型。</summary>
    NotificationChannel Channel { get; }

    /// <summary>
    /// 发送通知，返回是否成功与失败原因。
    /// </summary>
    Task<(bool Succeeded, string? FailReason)> SendAsync(NotificationRecord record, CancellationToken ct = default);
}
