using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// Outbox 域积压汇总项，对应一个限界上下文的积压状态。
/// </summary>
public sealed class OutboxContextSummary
{
    /// <summary>限界上下文，如 Order、Payment。</summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>积压事件数。</summary>
    public int PendingCount { get; init; }

    /// <summary>最早积压事件时间（UTC）。</summary>
    public DateTime? OldestPendingAt { get; init; }

    /// <summary>最大积压时长（分钟）。</summary>
    public long MaxAgeMinutes { get; init; }

    /// <summary>最近归档时间（UTC），可空。</summary>
    public DateTime? LastArchivedAt { get; init; }

    /// <summary>域状态。</summary>
    public OutboxContextStatus Status { get; init; }
}

/// <summary>
/// Outbox 积压趋势数据点。
/// </summary>
public sealed class OutboxTrendPoint
{
    /// <summary>时间戳（UTC）。</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>限界上下文。</summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>该时刻的积压事件数。</summary>
    public int PendingCount { get; init; }
}

/// <summary>
/// Outbox 重投结果。
/// </summary>
public sealed class OutboxRepublishResult
{
    /// <summary>成功重投数。</summary>
    public int SuccessCount { get; set; }

    /// <summary>失败重投数。</summary>
    public int FailureCount { get; set; }

    /// <summary>失败明细。</summary>
    public List<OutboxRepublishError> Errors { get; set; } = new();
}

/// <summary>
/// Outbox 重投失败明细。
/// </summary>
public sealed class OutboxRepublishError
{
    public Guid MessageId { get; init; }
    public string Error { get; init; } = string.Empty;
}

/// <summary>
/// Outbox 跨域查询服务抽象接口，封装对各域 outbox_messages 表的只读访问。
/// 默认实现 <see cref="Leno.SystemAdmin.Infrastructure.Services.OutboxQueryService"/> 通过配置化的连接字符串查询各域数据库。
/// 抽象便于测试时注入内存实现，也便于未来切换到统一观测平台（如 Prometheus + outbox_pending_count 指标）。
/// </summary>
public interface IOutboxQueryService
{
    /// <summary>
    /// 获取各域 Outbox 积压汇总。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>各域积压汇总列表。</returns>
    Task<List<OutboxContextSummary>> GetSummaryAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取近 N 小时积压趋势。
    /// </summary>
    /// <param name="hours">小时数，默认 24。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>趋势数据点列表（按上下文与时间分组）。</returns>
    Task<List<OutboxTrendPoint>> GetTrendAsync(int hours, CancellationToken ct = default);

    /// <summary>
    /// 分页查询指定域积压事件详情。
    /// </summary>
    /// <param name="context">限界上下文。</param>
    /// <param name="status">状态过滤，可空表示不限。</param>
    /// <param name="page">页码，从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>积压事件列表与总数。</returns>
    Task<OutboxMessageQueryResult> GetMessagesAsync(string context, string? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 批量重投指定域积压事件。
    /// 不传 messageIds 表示重投该域全部积压事件。
    /// </summary>
    /// <param name="context">限界上下文。</param>
    /// <param name="messageIds">消息标识列表；为空则重投全部。</param>
    /// <param name="operatorId">操作者标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>重投结果。</returns>
    Task<OutboxRepublishResult> RepublishAsync(string context, IReadOnlyCollection<Guid>? messageIds, string operatorId, CancellationToken ct = default);

    /// <summary>
    /// 归档指定域陈旧积压事件（CreatedAt 早于 before 的积压事件）。
    /// 归档后将记录写入 outbox_archive_records 表，并从原 outbox_messages 表删除（或标记为已归档）。
    /// </summary>
    /// <param name="context">限界上下文。</param>
    /// <param name="before">归档阈值（UTC）。</param>
    /// <param name="operatorId">操作者标识。</param>
    /// <param name="reason">归档原因。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>归档事件数量。</returns>
    Task<int> ArchiveAsync(string context, DateTime before, string operatorId, string reason, CancellationToken ct = default);
}

/// <summary>
/// Outbox 消息查询结果。
/// </summary>
public sealed class OutboxMessageQueryResult
{
    public List<OutboxMessageEntry> Items { get; init; } = new();
    public int Total { get; init; }
}
