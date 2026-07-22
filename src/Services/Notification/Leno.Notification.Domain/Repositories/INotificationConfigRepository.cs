using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Notification.Domain.Repositories;

/// <summary>
/// 通知渠道配置仓储接口。
/// 按 (Channel, ConfigKey) 业务唯一键索引，供 NotificationConfigAppService 持久化运营端配置变更。
/// </summary>
public interface INotificationConfigRepository : IRepository<NotificationConfig>
{
    /// <summary>按渠道 + 配置键查询配置项，不存在返回 null。</summary>
    Task<NotificationConfig?> GetAsync(NotificationChannel channel, string configKey, CancellationToken ct = default);

    /// <summary>查询指定渠道的全部配置项。</summary>
    Task<List<NotificationConfig>> GetByChannelAsync(NotificationChannel channel, CancellationToken ct = default);

    /// <summary>查询全部渠道配置项。</summary>
    Task<List<NotificationConfig>> GetAllAsync(CancellationToken ct = default);
}
