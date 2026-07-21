using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application.DTOs;

/// <summary>
/// 运营数据指标项 DTO，对应领域值对象 <see cref="MetricItem"/> 的对外投影。
/// </summary>
public sealed class MetricItemDto
{
    /// <summary>指标键，如 "total_gmv", "success_rate"。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>指标数值。</summary>
    public decimal Value { get; set; }

    /// <summary>指标单位，如 "CNY", "%", "次"。</summary>
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// 运营数据看板报表 DTO，对应 <see cref="Leno.SystemAdmin.Domain.Aggregates.DashboardReport"/> 聚合根的对外投影。
/// 仅暴露对外契约字段，不泄露聚合内部结构（Period/DataVersion 等内部字段不暴露）。
/// </summary>
public sealed class DashboardReportDto
{
    /// <summary>报表标识。</summary>
    public Guid ReportId { get; set; }

    /// <summary>报表类型。</summary>
    public ReportType ReportType { get; set; }

    /// <summary>统计粒度：hourly / daily / weekly。</summary>
    public string Granularity { get; set; } = string.Empty;

    /// <summary>报表生成时间（UTC）。</summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>报表覆盖的开始时间（UTC）。</summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>报表覆盖的结束时间（UTC）。</summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>指标项列表。</summary>
    public List<MetricItemDto> Metrics { get; set; } = [];
}

/// <summary>
/// 运营人员 DTO。
/// </summary>
public sealed class OperatorDto
{
    public Guid OperatorId { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public OperatorRole Role { get; set; }
    public List<string> Permissions { get; set; } = [];
    public OperatorStatus Status { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 运营人员分页结果 DTO。
/// </summary>
public sealed class OperatorListResultDto
{
    public List<OperatorDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 创建运营人员 DTO。
/// </summary>
public sealed class SaveOperatorDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public OperatorRole Role { get; set; }
    public List<string> Permissions { get; set; } = [];
}

/// <summary>
/// 分配权限 DTO。
/// </summary>
public sealed class AssignPermissionsDto
{
    public List<string> Permissions { get; set; } = [];
}

/// <summary>
/// 系统配置 DTO（加密配置的 Value 字段会被掩码为 "******"）。
/// </summary>
public sealed class SystemConfigDto
{
    public Guid ConfigId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEncrypted { get; set; }
    public ConfigStatus Status { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 系统配置分页结果 DTO。
/// </summary>
public sealed class SystemConfigListResultDto
{
    public List<SystemConfigDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 创建系统配置 DTO。
/// </summary>
public sealed class SaveSystemConfigDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEncrypted { get; set; }
}

/// <summary>
/// 更新系统配置 DTO（键不可变）。
/// </summary>
public sealed class UpdateSystemConfigDto
{
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEncrypted { get; set; }
}

/// <summary>
/// 审计日志 DTO。
/// </summary>
public sealed class AuditLogDto
{
    public Guid LogId { get; set; }
    public Guid OperatorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string? RequestSummary { get; set; }
    public int ResponseStatus { get; set; }
    public string? IpAddress { get; set; }
    public string? TraceId { get; set; }
    public DateTime OccurredAt { get; set; }
}

/// <summary>
/// 审计日志分页结果 DTO。
/// </summary>
public sealed class AuditLogListResultDto
{
    public List<AuditLogDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 操作日志 DTO。
/// </summary>
public sealed class OperationLogDto
{
    public Guid LogId { get; set; }
    public Guid OperatorId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BeforeSnapshot { get; set; }
    public string? AfterSnapshot { get; set; }
    public string? IpAddress { get; set; }
    public DateTime OccurredAt { get; set; }
}

/// <summary>
/// 操作日志分页结果 DTO。
/// </summary>
public sealed class OperationLogListResultDto
{
    public List<OperationLogDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 数据字典 DTO。
/// </summary>
public sealed class DataDictionaryDto
{
    public Guid DictionaryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DictionaryStatus Status { get; set; }
    public List<DictionaryItemDto> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 字典项 DTO。
/// </summary>
public sealed class DictionaryItemDto
{
    public Guid ItemId { get; set; }
    public Guid DictionaryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DictionaryStatus Status { get; set; }
}

/// <summary>
/// 数据字典分页结果 DTO。
/// </summary>
public sealed class DataDictionaryListResultDto
{
    public List<DataDictionaryDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 创建数据字典 DTO。
/// </summary>
public sealed class SaveDataDictionaryDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// 新增字典项 DTO。
/// </summary>
public sealed class AddDictionaryItemDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>
/// 更新字典项 DTO。
/// </summary>
public sealed class UpdateDictionaryItemDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>
/// 系统公告 DTO。
/// </summary>
public sealed class AnnouncementDto
{
    public Guid AnnouncementId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public AnnouncementType Type { get; set; }
    public AnnouncementTargetAudience TargetAudience { get; set; }
    public DateTime? PublishAt { get; set; }
    public DateTime? ExpireAt { get; set; }
    public AnnouncementStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 系统公告分页结果 DTO。
/// </summary>
public sealed class AnnouncementListResultDto
{
    public List<AnnouncementDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 创建/更新公告 DTO。
/// </summary>
public sealed class SaveAnnouncementDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public AnnouncementType Type { get; set; }
    public AnnouncementTargetAudience TargetAudience { get; set; }
    public DateTime? PublishAt { get; set; }
    public DateTime? ExpireAt { get; set; }
}

/// <summary>
/// 特性开关 DTO。
/// </summary>
public sealed class FeatureFlagDto
{
    public Guid FlagId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public FeatureFlagStrategy Strategy { get; set; }
    public string? Rules { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 特性开关分页结果 DTO。
/// </summary>
public sealed class FeatureFlagListResultDto
{
    public List<FeatureFlagDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 创建特性开关 DTO。
/// </summary>
public sealed class SaveFeatureFlagDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FeatureFlagStrategy Strategy { get; set; }
    public string? Rules { get; set; }
}

/// <summary>
/// 更新特性开关 DTO（键不可变）。
/// </summary>
public sealed class UpdateFeatureFlagDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FeatureFlagStrategy Strategy { get; set; }
    public string? Rules { get; set; }
}

/// <summary>
/// 特性开关评估请求 DTO。
/// </summary>
public sealed class EvaluateFlagDto
{
    public string FlagKey { get; set; } = string.Empty;
    public Dictionary<string, string> Context { get; set; } = [];
}

/// <summary>
/// 定时任务 DTO。
/// </summary>
public sealed class ScheduledTaskDto
{
    public Guid TaskId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string? Parameters { get; set; }
    public ScheduledTaskStatus Status { get; set; }
    public DateTime? LastRunAt { get; set; }
    public TaskRunStatus LastRunStatus { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 定时任务分页结果 DTO。
/// </summary>
public sealed class ScheduledTaskListResultDto
{
    public List<ScheduledTaskDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 创建定时任务 DTO。
/// </summary>
public sealed class SaveScheduledTaskDto
{
    public string Name { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string? Parameters { get; set; }
}

/// <summary>
/// 更新定时任务 DTO（作业类型不可变）。
/// </summary>
public sealed class UpdateScheduledTaskDto
{
    public string Name { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string? Parameters { get; set; }
}

/// <summary>
/// 索引重建任务 DTO。
/// </summary>
public sealed class IndexRebuildTaskDto
{
    public Guid TaskId { get; set; }
    public string TargetContext { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public RebuildTaskStatus Status { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// 索引重建任务分页结果 DTO。
/// </summary>
public sealed class IndexRebuildTaskListResultDto
{
    public List<IndexRebuildTaskDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 触发索引重建 DTO。
/// </summary>
public sealed class TriggerIndexRebuildDto
{
    public string TargetContext { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
}

/// <summary>
/// 死信消息 DTO。
/// </summary>
public sealed class DeadLetterMessageDto
{
    public Guid MessageId { get; set; }
    public string OriginalMessageId { get; set; } = string.Empty;
    public string SourceContext { get; set; } = string.Empty;
    public string OriginalTopic { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Headers { get; set; } = string.Empty;
    public string ErrorReason { get; set; } = string.Empty;
    public DeadLetterStatus Status { get; set; }
    public string? OperatorId { get; set; }
    public string? DiscardReason { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 死信消息分页结果 DTO。
/// </summary>
public sealed class DeadLetterListResultDto
{
    public List<DeadLetterMessageDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 丢弃死信消息 DTO。
/// </summary>
public sealed class DiscardDeadLetterDto
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 批量操作 DTO。
/// </summary>
public sealed class BatchOperationDto
{
    public List<Guid> MessageIds { get; set; } = [];
}

/// <summary>
/// 批量操作结果 DTO。
/// </summary>
public sealed class BatchOperationResultDto
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<BatchOperationErrorDto> Errors { get; set; } = [];
}

/// <summary>
/// 批量操作错误明细 DTO。
/// </summary>
public sealed class BatchOperationErrorDto
{
    public Guid MessageId { get; set; }
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// 模块健康详情 DTO。
/// </summary>
public sealed class ModuleHealthDto
{
    public string Module { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = [];
    public DateTime CheckedAt { get; set; }
    public long ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 健康聚合结果 DTO，包含整体状态与各模块详情。
/// </summary>
public sealed class HealthAggregationResultDto
{
    public string OverallStatus { get; set; } = string.Empty;
    public List<ModuleHealthDto> Modules { get; set; } = [];
    public DateTime AggregatedAt { get; set; }
}

// ============================================================
// SYS-05: 跨域审计日志条目 DTOs
// ============================================================

/// <summary>
/// 跨域审计日志条目 DTO。
/// </summary>
public sealed class AuditLogEntryDto
{
    public Guid EntryId { get; set; }
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Guid OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public string? RequestSummary { get; set; }
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>
/// 跨域审计日志条目分页结果 DTO。
/// </summary>
public sealed class AuditLogEntryListResultDto
{
    public List<AuditLogEntryDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

// ============================================================
// SYS-06: 接口限流规则 DTOs
// ============================================================

/// <summary>
/// 限流规则 DTO。
/// </summary>
public sealed class RateLimitRuleDto
{
    public Guid RuleId { get; set; }
    public string TargetApi { get; set; } = string.Empty;
    public string? TargetContext { get; set; }
    public int Limit { get; set; }
    public int WindowSeconds { get; set; }
    public LimitAlgorithm Algorithm { get; set; }
    public LimitScope Scope { get; set; }
    public bool Enabled { get; set; }
    public byte[] Version { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// 限流规则分页结果 DTO。
/// </summary>
public sealed class RateLimitRuleListResultDto
{
    public List<RateLimitRuleDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 创建/更新限流规则 DTO。
/// </summary>
public sealed class SaveRateLimitRuleDto
{
    public string TargetApi { get; set; } = string.Empty;
    public string? TargetContext { get; set; }
    public int Limit { get; set; }
    public int WindowSeconds { get; set; }
    public LimitAlgorithm Algorithm { get; set; }
    public LimitScope Scope { get; set; }
}
