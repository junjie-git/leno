using System;
using System.Collections.Generic;

namespace Leno.Notification.Domain.Channels;

/// <summary>
/// 通知渠道注册表领域服务接口，集中管理所有已注册渠道的元数据与能力声明。
/// 新增渠道只需"实现 <see cref="global::Leno.Notification.Domain.Services.INotificationChannel"/> + DI 注册"即可被注册表自动发现，
/// 无需修改调度器 / 选择器核心逻辑（零侵入核心调度）。
/// </summary>
public interface INotificationChannelRegistry
{
    /// <summary>
    /// 获取所有已注册渠道的元数据快照（按 Priority 升序）。
    /// </summary>
    IReadOnlyList<NotificationChannelMetadata> GetAllChannels();

    /// <summary>
    /// 按 Key 查找渠道元数据，未注册返回 null。
    /// </summary>
    NotificationChannelMetadata? GetChannel(ChannelKey key);

    /// <summary>
    /// 判断渠道是否已注册。
    /// </summary>
    bool IsRegistered(ChannelKey key);

    /// <summary>
    /// 按能力谓词过滤渠道（如查询所有 RequiresRateLimit=true 的渠道）。
    /// </summary>
    /// <param name="predicate">能力过滤谓词。</param>
    IEnumerable<NotificationChannelMetadata> GetChannelsByCapability(Func<NotificationChannelCapabilities, bool> predicate);
}
