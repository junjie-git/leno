using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application.DTOs;

// ============================================================
// Outbox 监控 DTOs
// ============================================================

/// <summary>
/// Outbox 域积压汇总 DTO，对应监控看板按域分组表格行。
/// </summary>
public sealed class OutboxContextSummaryDto
{
    /// <summary>限界上下文。</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>积压事件数。</summary>
    public int PendingCount { get; set; }

    /// <summary>最早积压事件时间（UTC）。</summary>
    public DateTime? OldestPendingAt { get; set; }

    /// <summary>最大积压时长（分钟）。</summary>
    public long MaxAgeMinutes { get; set; }

    /// <summary>最近归档时间（UTC），可空。</summary>
    public DateTime? LastArchivedAt { get; set; }

    /// <summary>域状态。</summary>
    public OutboxContextStatus Status { get; set; }
}

/// <summary>
/// Outbox 积压趋势数据点 DTO。
/// </summary>
public sealed class OutboxTrendPointDto
{
    /// <summary>时间戳（UTC）。</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>限界上下文。</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>该时刻的积压事件数。</summary>
    public int PendingCount { get; set; }
}

/// <summary>
/// Outbox 消息详情 DTO，对应详情抽屉事件列表行。
/// </summary>
public sealed class OutboxMessageDto
{
    /// <summary>消息标识。</summary>
    public Guid MessageId { get; set; }

    /// <summary>聚合根标识。</summary>
    public Guid AggregateId { get; set; }

    /// <summary>事件类型。</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>消息载荷（JSON）。</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>状态。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>重试次数。</summary>
    public int RetryCount { get; set; }

    /// <summary>错误信息，可空。</summary>
    public string? Error { get; set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>处理时间（UTC），可空。</summary>
    public DateTime? ProcessedAt { get; set; }
}

/// <summary>
/// Outbox 消息分页结果 DTO。
/// </summary>
public sealed class OutboxMessageListResultDto
{
    public List<OutboxMessageDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Outbox 归档历史 DTO。
/// </summary>
public sealed class OutboxArchiveHistoryDto
{
    /// <summary>归档记录标识。</summary>
    public Guid RecordId { get; set; }

    /// <summary>归档所属上下文。</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>归档事件数量。</summary>
    public int ArchivedCount { get; set; }

    /// <summary>归档阈值（UTC）。</summary>
    public DateTime ArchivedBefore { get; set; }

    /// <summary>归档时间（UTC）。</summary>
    public DateTime ArchivedAt { get; set; }

    /// <summary>归档操作人标识。</summary>
    public string ArchivedBy { get; set; } = string.Empty;

    /// <summary>归档原因。</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Outbox 归档历史分页结果 DTO。
/// </summary>
public sealed class OutboxArchiveHistoryListResultDto
{
    public List<OutboxArchiveHistoryDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 批量重投 Outbox 积压事件请求 DTO。
/// </summary>
public sealed class BatchRepublishOutboxDto
{
    /// <summary>消息标识列表；为空或不传表示重投全部积压事件。</summary>
    public List<Guid>? MessageIds { get; set; }
}

/// <summary>
/// 归档 Outbox 积压事件请求 DTO。
/// </summary>
public sealed class ArchiveOutboxDto
{
    /// <summary>归档阈值（UTC）：CreatedAt 早于此时间的积压事件将被归档。</summary>
    public DateTime Before { get; set; }

    /// <summary>归档原因。</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Outbox 重投结果 DTO。
/// </summary>
public sealed class OutboxRepublishResultDto
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<OutboxRepublishErrorDto> Errors { get; set; } = [];
}

/// <summary>
/// Outbox 重投失败明细 DTO。
/// </summary>
public sealed class OutboxRepublishErrorDto
{
    public Guid MessageId { get; set; }
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Outbox 归档结果 DTO。
/// </summary>
public sealed class OutboxArchiveResultDto
{
    /// <summary>本次归档事件数量。</summary>
    public int ArchivedCount { get; set; }

    /// <summary>归档记录标识。</summary>
    public Guid RecordId { get; set; }
}
