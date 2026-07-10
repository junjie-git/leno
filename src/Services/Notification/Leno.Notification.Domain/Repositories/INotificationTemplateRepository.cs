using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using NotificationTemplateAggregate = Leno.Notification.Domain.Aggregates.NotificationTemplate;

namespace Leno.Notification.Domain.Repositories;

/// <summary>
/// 通知模板仓储接口。
/// </summary>
public interface INotificationTemplateRepository : IRepository<NotificationTemplateAggregate>
{
    /// <summary>按事件类型与渠道查询启用态模板。</summary>
    Task<NotificationTemplateAggregate?> GetEnabledAsync(string eventType, NotificationChannel channel, CancellationToken ct = default);

    /// <summary>分页查询模板列表。</summary>
    Task<List<NotificationTemplateAggregate>> QueryAsync(string? eventType, NotificationChannel? channel, int page, int pageSize, CancellationToken ct = default);

    /// <summary>统计模板总数。</summary>
    Task<int> CountAsync(string? eventType, NotificationChannel? channel, CancellationToken ct = default);
}
