using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 跨域审计日志条目聚合根，汇总各领域集成事件产生的审计记录，仅追加不可变更。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>EntryId</c>。
/// </summary>
public sealed class AuditLogEntry : AggregateRoot
{
    private const int MaxEventTypeLength = 128;
    private const int MaxModuleLength = 64;
    private const int MaxActionLength = 128;
    private const int MaxOperatorNameLength = 128;
    private const int MaxRequestSummaryLength = 2000;
    private const int MaxIpAddressLength = 64;

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid EntryId => Id;

    /// <summary>来源集成事件的 EventId，用于幂等去重。</summary>
    public Guid EventId { get; private set; }

    /// <summary>事件类型名称（如 OrderCreatedEvent、PaymentSucceededEvent）。</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>来源聚合根标识。</summary>
    public Guid AggregateId { get; private set; }

    /// <summary>来源领域模块（如 Order、Payment、User）。</summary>
    public string Module { get; private set; } = string.Empty;

    /// <summary>操作动作描述。</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>操作人标识（用户域 UserId），无操作人时为 Guid.Empty。</summary>
    public Guid OperatorId { get; private set; }

    /// <summary>操作人名称（用户名/昵称），可空。</summary>
    public string? OperatorName { get; private set; }

    /// <summary>请求摘要，≤2000 字，可空。敏感数据已脱敏。</summary>
    public string? RequestSummary { get; private set; }

    /// <summary>审计发生时间（UTC）。</summary>
    public DateTime Timestamp { get; private set; }

    /// <summary>来源 IP 地址，≤64 字，可空。</summary>
    public string? IpAddress { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private AuditLogEntry() { }

    private AuditLogEntry(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验各字段长度与必填项，构建审计日志条目。仅追加，无更新方法。
    /// </summary>
    /// <param name="entryId">日志条目标识，由应用层生成。</param>
    /// <param name="eventId">来源集成事件的 EventId。</param>
    /// <param name="eventType">事件类型名称。</param>
    /// <param name="aggregateId">来源聚合根标识。</param>
    /// <param name="module">来源领域模块。</param>
    /// <param name="action">操作动作描述。</param>
    /// <param name="operatorId">操作人标识。</param>
    /// <param name="operatorName">操作人名称，可空。</param>
    /// <param name="requestSummary">请求摘要，可空。</param>
    /// <param name="timestamp">审计发生时间（UTC）。</param>
    /// <param name="ipAddress">来源 IP 地址，可空。</param>
    public static AuditLogEntry Create(
        Guid entryId,
        Guid eventId,
        string eventType,
        Guid aggregateId,
        string module,
        string action,
        Guid operatorId,
        string? operatorName,
        string? requestSummary,
        DateTime timestamp,
        string? ipAddress)
    {
        if (entryId == Guid.Empty)
        {
            throw new SystemAdminDomainException("日志条目标识不可为空", "AUDIT_ENTRY_ID_EMPTY");
        }

        if (eventId == Guid.Empty)
        {
            throw new SystemAdminDomainException("事件标识不可为空", "AUDIT_ENTRY_EVENT_ID_EMPTY");
        }

        ValidateEventType(eventType);
        ValidateModule(module);
        ValidateAction(action);
        ValidateOperatorName(operatorName);
        ValidateRequestSummary(requestSummary);
        ValidateIpAddress(ipAddress);

        if (timestamp == default)
        {
            throw new SystemAdminDomainException("审计时间不可为空", "AUDIT_ENTRY_TIMESTAMP_EMPTY");
        }

        return new AuditLogEntry(entryId)
        {
            EventId = eventId,
            EventType = eventType.Trim(),
            AggregateId = aggregateId,
            Module = module.Trim(),
            Action = action.Trim(),
            OperatorId = operatorId,
            OperatorName = NormalizeNullable(operatorName),
            RequestSummary = NormalizeNullable(requestSummary),
            Timestamp = timestamp,
            IpAddress = NormalizeNullable(ipAddress)
        };
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateEventType(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new SystemAdminDomainException("事件类型不可为空", "AUDIT_ENTRY_EVENT_TYPE_EMPTY");
        }

        if (eventType.Trim().Length > MaxEventTypeLength)
        {
            throw new SystemAdminDomainException($"事件类型长度不可超过 {MaxEventTypeLength} 字符", "AUDIT_ENTRY_EVENT_TYPE_LENGTH");
        }
    }

    private static void ValidateModule(string module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            throw new SystemAdminDomainException("模块不可为空", "AUDIT_ENTRY_MODULE_EMPTY");
        }

        if (module.Trim().Length > MaxModuleLength)
        {
            throw new SystemAdminDomainException($"模块长度不可超过 {MaxModuleLength} 字符", "AUDIT_ENTRY_MODULE_LENGTH");
        }
    }

    private static void ValidateAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new SystemAdminDomainException("操作动作不可为空", "AUDIT_ENTRY_ACTION_EMPTY");
        }

        if (action.Trim().Length > MaxActionLength)
        {
            throw new SystemAdminDomainException($"操作动作长度不可超过 {MaxActionLength} 字符", "AUDIT_ENTRY_ACTION_LENGTH");
        }
    }

    private static void ValidateOperatorName(string? operatorName)
    {
        if (!string.IsNullOrWhiteSpace(operatorName) && operatorName.Trim().Length > MaxOperatorNameLength)
        {
            throw new SystemAdminDomainException($"操作人名称长度不可超过 {MaxOperatorNameLength} 字符", "AUDIT_ENTRY_OPERATOR_NAME_LENGTH");
        }
    }

    private static void ValidateRequestSummary(string? requestSummary)
    {
        if (!string.IsNullOrWhiteSpace(requestSummary) && requestSummary.Trim().Length > MaxRequestSummaryLength)
        {
            throw new SystemAdminDomainException($"请求摘要长度不可超过 {MaxRequestSummaryLength} 字符", "AUDIT_ENTRY_REQUEST_SUMMARY_LENGTH");
        }
    }

    private static void ValidateIpAddress(string? ipAddress)
    {
        if (!string.IsNullOrWhiteSpace(ipAddress) && ipAddress.Trim().Length > MaxIpAddressLength)
        {
            throw new SystemAdminDomainException($"IP 地址长度不可超过 {MaxIpAddressLength} 字符", "AUDIT_ENTRY_IP_LENGTH");
        }
    }
}