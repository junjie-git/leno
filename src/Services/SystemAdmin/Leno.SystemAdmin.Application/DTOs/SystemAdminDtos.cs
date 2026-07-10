using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application.DTOs;

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
