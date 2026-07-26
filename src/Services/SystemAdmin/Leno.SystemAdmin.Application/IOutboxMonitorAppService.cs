using Leno.SystemAdmin.Application.DTOs;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// Outbox 监控应用服务接口，封装各域 Outbox 积压查询、重投、归档用例。
/// 委托 <see cref="Domain.Services.IOutboxQueryService"/> 跨域查询 outbox_messages 表。
/// </summary>
public interface IOutboxMonitorAppService
{
    /// <summary>获取各域 Outbox 积压汇总。</summary>
    Task<List<OutboxContextSummaryDto>> GetSummaryAsync(CancellationToken ct = default);

    /// <summary>获取近 N 小时积压趋势。</summary>
    Task<List<OutboxTrendPointDto>> GetTrendAsync(int hours = 24, CancellationToken ct = default);

    /// <summary>分页查询指定域积压事件详情。</summary>
    Task<OutboxMessageListResultDto> GetMessagesAsync(
        string context,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>批量重投指定域积压事件。messageIds 为空则重投全部。</summary>
    Task<OutboxRepublishResultDto> RepublishAsync(
        string context,
        List<Guid>? messageIds,
        string operatorId,
        CancellationToken ct = default);

    /// <summary>归档指定域陈旧积压事件（CreatedAt 早于 before 的事件）。</summary>
    Task<OutboxArchiveResultDto> ArchiveAsync(
        string context,
        DateTime before,
        string operatorId,
        string reason,
        CancellationToken ct = default);

    /// <summary>分页查询指定域归档历史。</summary>
    Task<OutboxArchiveHistoryListResultDto> GetArchiveHistoryAsync(
        string context,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
