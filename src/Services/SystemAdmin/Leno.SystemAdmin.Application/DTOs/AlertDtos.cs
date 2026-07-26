using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application.DTOs;

// ============================================================
// 告警管理 DTOs
// ============================================================

/// <summary>
/// 告警事件 DTO，对应列表页表格行。
/// </summary>
public sealed class AlertDto
{
    /// <summary>告警标识。</summary>
    public Guid AlertId { get; set; }

    /// <summary>告警名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>来源模块。</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>严重级别。</summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>告警状态。</summary>
    public AlertStatus Status { get; set; }

    /// <summary>触发时间（UTC）。</summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>持续时长（秒）。</summary>
    public long DurationSeconds { get; set; }

    /// <summary>摘要。</summary>
    public string? Summary { get; set; }
}

/// <summary>
/// 告警详情 DTO，对应详情抽屉全字段展示。
/// </summary>
public sealed class AlertDetailDto
{
    /// <summary>告警标识。</summary>
    public Guid AlertId { get; set; }

    /// <summary>告警名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>来源模块。</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>严重级别。</summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>告警状态。</summary>
    public AlertStatus Status { get; set; }

    /// <summary>触发时间（UTC）。</summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>持续时长（秒）。</summary>
    public long DurationSeconds { get; set; }

    /// <summary>标签集合。</summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>注释集合。</summary>
    public Dictionary<string, string> Annotations { get; set; } = new();

    /// <summary>关联指标名。</summary>
    public string? RelatedMetric { get; set; }

    /// <summary>摘要。</summary>
    public string? Summary { get; set; }

    /// <summary>详细描述。</summary>
    public string? Description { get; set; }

    /// <summary>确认时间（UTC），可空。</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>确认人标识，可空。</summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary>确认备注，可空。</summary>
    public string? AcknowledgeComment { get; set; }
}

/// <summary>
/// 告警分页结果 DTO。
/// </summary>
public sealed class AlertListResultDto
{
    public List<AlertDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 确认告警请求 DTO。
/// </summary>
public sealed class AcknowledgeAlertDto
{
    public string? Comment { get; set; }
}

/// <summary>
/// 静默规则 DTO。
/// </summary>
public sealed class AlertSilenceDto
{
    /// <summary>静默规则标识。</summary>
    public Guid SilenceId { get; set; }

    /// <summary>匹配器 JSON 数组。</summary>
    public string Matchers { get; set; } = "[]";

    /// <summary>持续时长描述，如 "2h"。</summary>
    public string Duration { get; set; } = string.Empty;

    /// <summary>静默原因。</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>起始时间（UTC）。</summary>
    public DateTime StartsAt { get; set; }

    /// <summary>结束时间（UTC）。</summary>
    public DateTime EndsAt { get; set; }

    /// <summary>创建人标识。</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>是否已过期。</summary>
    public bool IsExpired { get; set; }
}

/// <summary>
/// 静默规则列表结果 DTO。
/// </summary>
public sealed class AlertSilenceListResultDto
{
    public List<AlertSilenceDto> Items { get; set; } = [];
}

/// <summary>
/// 创建静默规则请求 DTO。
/// </summary>
public sealed class CreateAlertSilenceDto
{
    /// <summary>匹配器数组，格式 [{"name":"module","value":"Payment","isRegex":false}]。</summary>
    public List<MatcherItemDto> Matchers { get; set; } = [];

    /// <summary>持续时长描述，如 "2h"、"1d"。</summary>
    public string Duration { get; set; } = string.Empty;

    /// <summary>静默原因。</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 静默规则匹配器 DTO。
/// </summary>
public sealed class MatcherItemDto
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsRegex { get; set; }
}
