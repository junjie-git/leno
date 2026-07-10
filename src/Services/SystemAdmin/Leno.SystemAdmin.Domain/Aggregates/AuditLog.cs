using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 审计日志聚合根，记录运营人员对资源的操作请求，仅追加不可变更。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>LogId</c>。
/// </summary>
public sealed class AuditLog : AggregateRoot
{
    private const int MaxActionLength = 128;
    private const int MaxResourceTypeLength = 64;
    private const int MaxResourceIdLength = 64;
    private const int MaxRequestSummaryLength = 2000;
    private const int MaxIpAddressLength = 64;
    private const int MaxTraceIdLength = 64;

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid LogId => Id;

    /// <summary>操作运营人员标识。</summary>
    public Guid OperatorId { get; private set; }

    /// <summary>操作动作，≤128 字。</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>资源类型，≤64 字。</summary>
    public string ResourceType { get; private set; } = string.Empty;

    /// <summary>资源标识，≤64 字。</summary>
    public string ResourceId { get; private set; } = string.Empty;

    /// <summary>请求摘要，≤2000 字，可空。</summary>
    public string? RequestSummary { get; private set; }

    /// <summary>响应状态码。</summary>
    public int ResponseStatus { get; private set; }

    /// <summary>来源 IP 地址，≤64 字，可空。</summary>
    public string? IpAddress { get; private set; }

    /// <summary>链路追踪标识，≤64 字，可空。</summary>
    public string? TraceId { get; private set; }

    /// <summary>审计发生时间（UTC）。</summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private AuditLog() { }

    private AuditLog(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验各字段长度与必填项，构建审计日志。审计日志仅追加，无更新方法。
    /// </summary>
    /// <param name="logId">日志标识，由应用层生成。</param>
    /// <param name="operatorId">操作运营人员标识。</param>
    /// <param name="action">操作动作。</param>
    /// <param name="resourceType">资源类型。</param>
    /// <param name="resourceId">资源标识。</param>
    /// <param name="requestSummary">请求摘要，可空。</param>
    /// <param name="responseStatus">响应状态码。</param>
    /// <param name="ipAddress">来源 IP 地址，可空。</param>
    /// <param name="traceId">链路追踪标识，可空。</param>
    /// <param name="occurredAt">审计发生时间（UTC）。</param>
    public static AuditLog Create(
        Guid logId,
        Guid operatorId,
        string action,
        string resourceType,
        string resourceId,
        string? requestSummary,
        int responseStatus,
        string? ipAddress,
        string? traceId,
        DateTime occurredAt)
    {
        if (logId == Guid.Empty)
        {
            throw new SystemAdminDomainException("日志标识不可为空", "AUDIT_LOG_ID_EMPTY");
        }

        if (operatorId == Guid.Empty)
        {
            throw new SystemAdminDomainException("运营人员标识不可为空", "AUDIT_OPERATOR_EMPTY");
        }

        ValidateAction(action);
        ValidateResourceType(resourceType);
        ValidateResourceId(resourceId);
        ValidateRequestSummary(requestSummary);
        ValidateIpAddress(ipAddress);
        ValidateTraceId(traceId);

        if (occurredAt == default)
        {
            throw new SystemAdminDomainException("审计发生时间不可为空", "AUDIT_OCCURRED_AT_EMPTY");
        }

        return new AuditLog(logId)
        {
            OperatorId = operatorId,
            Action = action.Trim(),
            ResourceType = resourceType.Trim(),
            ResourceId = resourceId.Trim(),
            RequestSummary = NormalizeNullable(requestSummary),
            ResponseStatus = responseStatus,
            IpAddress = NormalizeNullable(ipAddress),
            TraceId = NormalizeNullable(traceId),
            OccurredAt = occurredAt
        };
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new SystemAdminDomainException("操作动作不可为空", "AUDIT_ACTION_EMPTY");
        }

        if (action.Trim().Length > MaxActionLength)
        {
            throw new SystemAdminDomainException($"操作动作长度不可超过 {MaxActionLength} 字符", "AUDIT_ACTION_LENGTH");
        }
    }

    private static void ValidateResourceType(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            throw new SystemAdminDomainException("资源类型不可为空", "AUDIT_RESOURCE_TYPE_EMPTY");
        }

        if (resourceType.Trim().Length > MaxResourceTypeLength)
        {
            throw new SystemAdminDomainException($"资源类型长度不可超过 {MaxResourceTypeLength} 字符", "AUDIT_RESOURCE_TYPE_LENGTH");
        }
    }

    private static void ValidateResourceId(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new SystemAdminDomainException("资源标识不可为空", "AUDIT_RESOURCE_ID_EMPTY");
        }

        if (resourceId.Trim().Length > MaxResourceIdLength)
        {
            throw new SystemAdminDomainException($"资源标识长度不可超过 {MaxResourceIdLength} 字符", "AUDIT_RESOURCE_ID_LENGTH");
        }
    }

    private static void ValidateRequestSummary(string? requestSummary)
    {
        if (!string.IsNullOrWhiteSpace(requestSummary) && requestSummary.Trim().Length > MaxRequestSummaryLength)
        {
            throw new SystemAdminDomainException($"请求摘要长度不可超过 {MaxRequestSummaryLength} 字符", "AUDIT_REQUEST_SUMMARY_LENGTH");
        }
    }

    private static void ValidateIpAddress(string? ipAddress)
    {
        if (!string.IsNullOrWhiteSpace(ipAddress) && ipAddress.Trim().Length > MaxIpAddressLength)
        {
            throw new SystemAdminDomainException($"IP 地址长度不可超过 {MaxIpAddressLength} 字符", "AUDIT_IP_LENGTH");
        }
    }

    private static void ValidateTraceId(string? traceId)
    {
        if (!string.IsNullOrWhiteSpace(traceId) && traceId.Trim().Length > MaxTraceIdLength)
        {
            throw new SystemAdminDomainException($"TraceId 长度不可超过 {MaxTraceIdLength} 字符", "AUDIT_TRACE_LENGTH");
        }
    }
}
