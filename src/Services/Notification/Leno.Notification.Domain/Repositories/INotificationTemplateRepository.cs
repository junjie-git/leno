using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using NotificationTemplateAggregate = Leno.Notification.Domain.Aggregates.NotificationTemplate;

namespace Leno.Notification.Domain.Repositories;

/// <summary>
/// 通知模板仓储接口。
/// </summary>
public interface INotificationTemplateRepository : IRepository<NotificationTemplateAggregate>
{
    /// <summary>按模板编码与渠道查询启用态模板。</summary>
    Task<NotificationTemplateAggregate?> GetEnabledAsync(string code, NotificationChannel channel, CancellationToken ct = default);

    /// <summary>分页查询模板列表。</summary>
    Task<List<NotificationTemplateAggregate>> QueryAsync(string? code, NotificationChannel? channel, int page, int pageSize, CancellationToken ct = default);

    /// <summary>统计模板总数。</summary>
    Task<int> CountAsync(string? code, NotificationChannel? channel, CancellationToken ct = default);

    /// <summary>按模板编码查询首个启用态模板（不限制渠道）。</summary>
    Task<NotificationTemplateAggregate?> GetEnabledByCodeAsync(string code, CancellationToken ct = default);
}
