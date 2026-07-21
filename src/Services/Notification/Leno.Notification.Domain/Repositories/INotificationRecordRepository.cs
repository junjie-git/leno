using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using NotificationRecordAggregate = Leno.Notification.Domain.Aggregates.NotificationRecord;

namespace Leno.Notification.Domain.Repositories;

/// <summary>
/// 通知记录仓储接口。
/// </summary>
public interface INotificationRecordRepository : IRepository<NotificationRecordAggregate>
{
    /// <summary>按事件标识查询是否已存在通知记录（幂等去重）。</summary>
    Task<bool> ExistsByEventIdAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>按 ID 列表批量查询通知记录，避免 N+1 查询。</summary>
    Task<List<NotificationRecordAggregate>> GetByIdsAsync(List<Guid> ids, CancellationToken ct = default);

    /// <summary>按用户分页查询站内信（仅 InApp 渠道）。</summary>
    Task<List<NotificationRecordAggregate>> QueryByUserAsync(Guid userId, bool? isRead, int page, int pageSize, CancellationToken ct = default);

    /// <summary>按用户统计站内信总数。</summary>
    Task<int> CountByUserAsync(Guid userId, bool? isRead, CancellationToken ct = default);

    /// <summary>查询待发送通知（状态为 Pending）。</summary>
    Task<List<NotificationRecordAggregate>> GetPendingAsync(int limit, CancellationToken ct = default);

    /// <summary>查询可重试的失败通知（状态为 Failed 且 RetryCount &lt; DefaultMaxRetry）。</summary>
    Task<List<NotificationRecordAggregate>> GetRetryableAsync(int limit, CancellationToken ct = default);

    /// <summary>按用户批量标记已读。</summary>
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>按幂等键查询通知记录（幂等去重）。</summary>
    Task<NotificationRecordAggregate?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>查询已安排重试且 NextRetryAt 已到期的通知记录。</summary>
    Task<List<NotificationRecordAggregate>> GetRetriedWithExpiredNextRetryAsync(int limit, CancellationToken ct = default);

    /// <summary>查询死信通知记录（分页）。</summary>
    Task<List<NotificationRecordAggregate>> GetDeadLetteredAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>统计死信通知记录总数。</summary>
    Task<int> CountDeadLetteredAsync(CancellationToken ct = default);

    /// <summary>按渠道消息标识查询通知记录（用于回执匹配）。</summary>
    Task<NotificationRecordAggregate?> GetByChannelMessageIdAsync(string channelMessageId, CancellationToken ct = default);

    /// <summary>多维度分页查询通知记录（管理员端）。</summary>
    Task<List<NotificationRecordAggregate>> QueryRecordsAsync(
        Guid? userId, NotificationChannel? channel, NotificationStatus? status,
        string? templateCode, string? businessRef, DateTime? fromTime, DateTime? toTime,
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>多维度查询统计总数。</summary>
    Task<int> CountRecordsAsync(
        Guid? userId, NotificationChannel? channel, NotificationStatus? status,
        string? templateCode, string? businessRef, DateTime? fromTime, DateTime? toTime,
        CancellationToken ct = default);

    /// <summary>按业务引用标识查询通知记录。</summary>
    Task<List<NotificationRecordAggregate>> GetByBusinessRefAsync(string businessRef, CancellationToken ct = default);

    /// <summary>获取送达率统计（按渠道和模板分组）。</summary>
    Task<List<DeliveryStatistics>> GetDeliveryStatisticsAsync(DateTime? fromTime, DateTime? toTime, CancellationToken ct = default);
}

/// <summary>
/// 送达率统计结果。
/// </summary>
public sealed class DeliveryStatistics
{
    public NotificationChannel Channel { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public int DeadLetteredCount { get; set; }
    public double DeliveryRate => TotalCount > 0 ? (double)SucceededCount / TotalCount : 0;
}
