using System.ComponentModel;
using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Domain.Exceptions;

namespace Leno.UserAuth.Domain.Aggregates;

/// <summary>
/// 审计日志聚合根，记录管理员关键操作的不可变审计记录。
/// 只读追加，不可修改不可删除；仅用于事务内写入。
/// 跨域审计日志聚合查询由 BC11 系统管理域（F-SYS-009）承载。
/// </summary>
public sealed class AuditLog : AggregateRoot
{
    /// <summary>操作人标识，引用 User。</summary>
    public Guid OperatorId { get; private set; }

    /// <summary>操作类型（如 UserBan/UserUnban/AccountDisable/RoleAssign/RoleRevoke/PermissionChange）。</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>目标资源类型（如 User/Role/OAuthClient）。</summary>
    public string ResourceType { get; private set; } = string.Empty;

    /// <summary>目标资源标识，可空（列表类操作无具体资源）。</summary>
    public string? ResourceId { get; private set; }

    /// <summary>操作前快照（JSON），可空。</summary>
    public string? BeforeSnapshot { get; private set; }

    /// <summary>操作后快照（JSON），可空。</summary>
    public string? AfterSnapshot { get; private set; }

    /// <summary>操作时间（UTC）。</summary>
    public DateTime OperatedAt { get; private set; }

    /// <summary>操作来源 IP，可空。</summary>
    public string? Ip { get; private set; }

    /// <summary>操作来源 User-Agent，可空。</summary>
    public string? UserAgent { get; private set; }

    /// <summary>链路追踪标识，可空。</summary>
    public string? TraceId { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private AuditLog() { }

    private AuditLog(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建审计日志记录。审计日志仅追加写入，无更新与删除方法。
    /// </summary>
    public static AuditLog Create(
        Guid id,
        Guid operatorId,
        string action,
        string resourceType,
        string? resourceId = null,
        string? beforeSnapshot = null,
        string? afterSnapshot = null,
        string? ip = null,
        string? userAgent = null,
        string? traceId = null)
    {
        if (id == Guid.Empty)
        {
            throw new UserAuthDomainException("审计日志标识不可为空", "AUDIT_LOG_ID_EMPTY");
        }

        if (operatorId == Guid.Empty)
        {
            throw new UserAuthDomainException("操作人标识不可为空", "AUDIT_LOG_OPERATOR_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new UserAuthDomainException("操作类型不可为空", "AUDIT_LOG_ACTION_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(resourceType))
        {
            throw new UserAuthDomainException("目标资源类型不可为空", "AUDIT_LOG_RESOURCE_TYPE_EMPTY");
        }

        return new AuditLog(id)
        {
            OperatorId = operatorId,
            Action = action.Trim(),
            ResourceType = resourceType.Trim(),
            ResourceId = string.IsNullOrWhiteSpace(resourceId) ? null : resourceId.Trim(),
            BeforeSnapshot = beforeSnapshot,
            AfterSnapshot = afterSnapshot,
            OperatedAt = DateTime.UtcNow,
            Ip = string.IsNullOrWhiteSpace(ip) ? null : ip.Trim(),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim(),
            TraceId = string.IsNullOrWhiteSpace(traceId) ? null : traceId.Trim()
        };
    }

    /// <summary>
    /// 在保存变更前由 <c>AuditLogInterceptor</c> 调用，从当前 HTTP 上下文补充技术字段（IP / UA / TraceId）。
    /// 仅当字段为空时写入，不覆盖应用服务已显式设置的值。
    /// 标注 <see cref="EditorBrowsableState.Never"/> 以避免被业务代码误用，仅基础设施层调用。
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Enrich(string? ip, string? userAgent, string? traceId)
    {
        if (Ip is null && !string.IsNullOrWhiteSpace(ip))
        {
            Ip = ip.Trim();
        }

        if (UserAgent is null && !string.IsNullOrWhiteSpace(userAgent))
        {
            UserAgent = userAgent.Trim();
        }

        if (TraceId is null && !string.IsNullOrWhiteSpace(traceId))
        {
            TraceId = traceId.Trim();
        }
    }
}
