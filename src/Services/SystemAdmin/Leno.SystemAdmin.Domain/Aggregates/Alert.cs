using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 告警事件聚合根，对应 Alertmanager 告警事件的领域投影。
/// 状态流转：Firing → Acknowledged → Resolved。
/// 该聚合为只读消费视图，由 <see cref="Services.IAlertmanagerClient"/> 拉取后构建，不直接落库。
/// </summary>
public sealed class Alert : AggregateRoot
{
    private const int MaxNameLength = 256;
    private const int MaxModuleLength = 128;
    private const int MaxSummaryLength = 1024;
    private const int MaxDescriptionLength = 4096;
    private const int MaxRelatedMetricLength = 512;
    private const int MaxAckCommentLength = 1000;
    private const int MaxOperatorIdLength = 64;

    /// <summary>告警名称，对应 Alertmanager alertname 标签。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>来源模块，如 Payment、Order。</summary>
    public string Module { get; private set; } = string.Empty;

    /// <summary>严重级别。</summary>
    public AlertSeverity Severity { get; private set; }

    /// <summary>告警状态。</summary>
    public AlertStatus Status { get; private set; }

    /// <summary>标签集合（key=value），用于筛选与静默匹配。</summary>
    public Dictionary<string, string> Labels { get; private set; } = new();

    /// <summary>注释集合（key=value），包含告警摘要、描述等元信息。</summary>
    public Dictionary<string, string> Annotations { get; private set; } = new();

    /// <summary>关联指标名，用于跳转 Prometheus 指标图。</summary>
    public string? RelatedMetric { get; private set; }

    /// <summary>摘要。</summary>
    public string? Summary { get; private set; }

    /// <summary>详细描述。</summary>
    public string? Description { get; private set; }

    /// <summary>触发时间（UTC）。</summary>
    public DateTime TriggeredAt { get; private set; }

    /// <summary>持续时长（秒）。</summary>
    public long DurationSeconds { get; private set; }

    /// <summary>确认时间（UTC），可空。</summary>
    public DateTime? AcknowledgedAt { get; private set; }

    /// <summary>确认人标识，可空。</summary>
    public string? AcknowledgedBy { get; private set; }

    /// <summary>确认备注，可空。</summary>
    public string? AcknowledgeComment { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Alert() { }

    private Alert(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验字段并构建告警事件聚合，初始状态由参数指定。
    /// </summary>
    /// <param name="id">告警标识。</param>
    /// <param name="name">告警名称。</param>
    /// <param name="module">来源模块。</param>
    /// <param name="severity">严重级别。</param>
    /// <param name="status">告警状态。</param>
    /// <param name="labels">标签集合。</param>
    /// <param name="annotations">注释集合。</param>
    /// <param name="relatedMetric">关联指标名。</param>
    /// <param name="summary">摘要。</param>
    /// <param name="description">详细描述。</param>
    /// <param name="triggeredAt">触发时间（UTC）。</param>
    /// <param name="durationSeconds">持续时长（秒）。</param>
    /// <param name="acknowledgedAt">确认时间（UTC），可空。</param>
    /// <param name="acknowledgedBy">确认人标识，可空。</param>
    /// <param name="acknowledgeComment">确认备注，可空。</param>
    public static Alert Create(
        Guid id,
        string name,
        string module,
        AlertSeverity severity,
        AlertStatus status,
        Dictionary<string, string> labels,
        Dictionary<string, string> annotations,
        string? relatedMetric,
        string? summary,
        string? description,
        DateTime triggeredAt,
        long durationSeconds,
        DateTime? acknowledgedAt = null,
        string? acknowledgedBy = null,
        string? acknowledgeComment = null)
    {
        if (id == Guid.Empty)
        {
            throw new SystemAdminDomainException("告警标识不可为空", "ALERT_ID_EMPTY");
        }
        ValidateName(name);
        ValidateModule(module);
        ValidateRelatedMetric(relatedMetric);
        ValidateSummary(summary);
        ValidateDescription(description);
        ValidateDurationSeconds(durationSeconds);
        ValidateAckComment(acknowledgeComment);
        ValidateOperatorId(acknowledgedBy);

        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(annotations);

        return new Alert(id)
        {
            Name = name.Trim(),
            Module = module.Trim(),
            Severity = severity,
            Status = status,
            Labels = new Dictionary<string, string>(labels, StringComparer.Ordinal),
            Annotations = new Dictionary<string, string>(annotations, StringComparer.Ordinal),
            RelatedMetric = string.IsNullOrWhiteSpace(relatedMetric) ? null : relatedMetric!.Trim(),
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary!.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description!.Trim(),
            TriggeredAt = triggeredAt,
            DurationSeconds = durationSeconds,
            AcknowledgedAt = acknowledgedAt,
            AcknowledgedBy = string.IsNullOrWhiteSpace(acknowledgedBy) ? null : acknowledgedBy!.Trim(),
            AcknowledgeComment = string.IsNullOrWhiteSpace(acknowledgeComment) ? null : acknowledgeComment!.Trim()
        };
    }

    /// <summary>
    /// 确认告警，仅 Firing 态可确认；已确认则幂等返回当前状态；已恢复则禁止确认。
    /// </summary>
    /// <param name="operatorId">操作者标识。</param>
    /// <param name="comment">确认备注，可空。</param>
    public void Acknowledge(string operatorId, string? comment)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new SystemAdminDomainException("操作者标识不可为空", "ALERT_ACK_OPERATOR_EMPTY");
        }
        ValidateOperatorId(operatorId);
        ValidateAckComment(comment);

        if (Status == AlertStatus.Resolved)
        {
            throw new SystemAdminDomainException("已恢复的告警不可确认", "ALERT_ALREADY_RESOLVED");
        }

        if (Status == AlertStatus.Acknowledged)
        {
            return;
        }

        Status = AlertStatus.Acknowledged;
        AcknowledgedAt = DateTime.UtcNow;
        AcknowledgedBy = operatorId.Trim();
        AcknowledgeComment = string.IsNullOrWhiteSpace(comment) ? null : comment!.Trim();
    }

    /// <summary>
    /// 标记告警为已恢复（由 Alertmanager 推送 resolved 事件触发）。
    /// </summary>
    public void Resolve()
    {
        if (Status == AlertStatus.Resolved)
        {
            return;
        }

        Status = AlertStatus.Resolved;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SystemAdminDomainException("告警名称不可为空", "ALERT_NAME_EMPTY");
        }
        if (name.Trim().Length > MaxNameLength)
        {
            throw new SystemAdminDomainException($"告警名称长度不可超过 {MaxNameLength} 字符", "ALERT_NAME_LENGTH");
        }
    }

    private static void ValidateModule(string module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            throw new SystemAdminDomainException("来源模块不可为空", "ALERT_MODULE_EMPTY");
        }
        if (module.Trim().Length > MaxModuleLength)
        {
            throw new SystemAdminDomainException($"来源模块长度不可超过 {MaxModuleLength} 字符", "ALERT_MODULE_LENGTH");
        }
    }

    private static void ValidateRelatedMetric(string? relatedMetric)
    {
        if (!string.IsNullOrWhiteSpace(relatedMetric) && relatedMetric.Trim().Length > MaxRelatedMetricLength)
        {
            throw new SystemAdminDomainException($"关联指标名长度不可超过 {MaxRelatedMetricLength} 字符", "ALERT_RELATED_METRIC_LENGTH");
        }
    }

    private static void ValidateSummary(string? summary)
    {
        if (!string.IsNullOrWhiteSpace(summary) && summary.Trim().Length > MaxSummaryLength)
        {
            throw new SystemAdminDomainException($"摘要长度不可超过 {MaxSummaryLength} 字符", "ALERT_SUMMARY_LENGTH");
        }
    }

    private static void ValidateDescription(string? description)
    {
        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength)
        {
            throw new SystemAdminDomainException($"详细描述长度不可超过 {MaxDescriptionLength} 字符", "ALERT_DESCRIPTION_LENGTH");
        }
    }

    private static void ValidateDurationSeconds(long durationSeconds)
    {
        if (durationSeconds < 0)
        {
            throw new SystemAdminDomainException("持续时长不可为负数", "ALERT_DURATION_NEGATIVE");
        }
    }

    private static void ValidateAckComment(string? comment)
    {
        if (!string.IsNullOrWhiteSpace(comment) && comment.Trim().Length > MaxAckCommentLength)
        {
            throw new SystemAdminDomainException($"确认备注长度不可超过 {MaxAckCommentLength} 字符", "ALERT_ACK_COMMENT_LENGTH");
        }
    }

    private static void ValidateOperatorId(string? operatorId)
    {
        if (!string.IsNullOrWhiteSpace(operatorId) && operatorId.Trim().Length > MaxOperatorIdLength)
        {
            throw new SystemAdminDomainException($"操作者标识长度不可超过 {MaxOperatorIdLength} 字符", "ALERT_OPERATOR_LENGTH");
        }
    }
}
