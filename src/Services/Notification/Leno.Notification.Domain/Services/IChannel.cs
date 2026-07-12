using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Services;

/// <summary>
/// 通知发送渠道接口，各渠道实现具体发送逻辑。
/// 定义在领域层，由基础设施层实现。
/// </summary>
public interface INotificationChannel
{
    /// <summary>渠道类型。</summary>
    NotificationChannel Channel { get; }

    /// <summary>
    /// 发送通知，返回发送结果。
    /// </summary>
    Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default);
}