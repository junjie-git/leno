using Leno.Notification.Domain.Channels;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Services;

/// <summary>
/// 通知发送渠道接口，各渠道实现具体发送逻辑。
/// 定义在领域层，由基础设施层实现。
/// 渠道自描述 <see cref="ChannelKey"/> 与 <see cref="Metadata"/>，由 <see cref="INotificationChannelRegistry"/> 汇总后供调度器 / 限流器查询。
/// 新增渠道只需实现本接口并注册到 DI，即可被注册表自动发现，零侵入核心调度逻辑。
/// </summary>
public interface INotificationChannel
{
    /// <summary>渠道类型（向后兼容，旧调度路径仍按枚举查找）。</summary>
    NotificationChannel Channel { get; }

    /// <summary>
    /// 渠道 Key 强类型字符串，注册表唯一标识，替代 <see cref="Channel"/> 枚举用于偏好存储与注册表查询。
    /// </summary>
    ChannelKey ChannelKey { get; }

    /// <summary>
    /// 渠道自描述元数据（能力 + 优先级 + 启用状态）。
    /// </summary>
    NotificationChannelMetadata Metadata { get; }

    /// <summary>
    /// 发送通知，返回发送结果。
    /// </summary>
    Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default);
}
