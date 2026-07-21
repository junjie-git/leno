using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Notification.Domain.Repositories;

/// <summary>
/// 通知限流配置仓储接口。
/// </summary>
public interface INotificationRateLimitConfigRepository : IRepository<NotificationRateLimitConfig>
{
    /// <summary>按渠道查询限流配置，不存在返回 null。</summary>
    Task<NotificationRateLimitConfig?> GetByChannelAsync(NotificationChannel channel, CancellationToken ct = default);

    /// <summary>查询所有渠道限流配置。</summary>
    Task<List<NotificationRateLimitConfig>> GetAllAsync(CancellationToken ct = default);
}
