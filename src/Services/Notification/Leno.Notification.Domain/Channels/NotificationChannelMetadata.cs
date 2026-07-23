namespace Leno.Notification.Domain.Channels;

/// <summary>
/// 通知渠道元数据，渠道自描述信息。
/// 由 <see cref="INotificationChannel"/> 实现提供，注册表汇总后供调度器 / 限流器 / 偏好查询使用。
/// </summary>
/// <param name="Key">渠道 Key，注册表唯一标识。</param>
/// <param name="DisplayName">渠道展示名称（中文，用于运营后台 / 偏好配置 UI）。</param>
/// <param name="Capabilities">渠道能力声明。</param>
/// <param name="IsEnabled">渠道是否启用（运营端动态开关，false 时调度器跳过）。</param>
/// <param name="Priority">渠道优先级，数值越小优先级越高（10/20/30，用于多渠道并存时的排序）。</param>
public sealed record NotificationChannelMetadata(
    ChannelKey Key,
    string DisplayName,
    NotificationChannelCapabilities Capabilities,
    bool IsEnabled,
    int Priority);
