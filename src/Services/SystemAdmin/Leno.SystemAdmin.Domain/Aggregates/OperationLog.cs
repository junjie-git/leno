using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 操作日志聚合根，记录运营人员对业务数据的变更前后快照，仅追加不可变更。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>LogId</c>。
/// </summary>
public sealed class OperationLog : AggregateRoot
{
    private const int MaxOperationTypeLength = 64;
    private const int MaxModuleLength = 64;
    private const int MaxDescriptionLength = 500;
    private const int MaxSnapshotLength = 4000;
    private const int MaxIpAddressLength = 64;

    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid LogId => Id;

    /// <summary>操作运营人员标识。</summary>
    public Guid OperatorId { get; private set; }

    /// <summary>操作类型，≤64 字。</summary>
    public string OperationType { get; private set; } = string.Empty;

    /// <summary>所属模块，≤64 字。</summary>
    public string Module { get; private set; } = string.Empty;

    /// <summary>操作描述，≤500 字，可空。</summary>
    public string? Description { get; private set; }

    /// <summary>变更前快照（JSON），≤4000 字，可空。</summary>
    public string? BeforeSnapshot { get; private set; }

    /// <summary>变更后快照（JSON），≤4000 字，可空。</summary>
    public string? AfterSnapshot { get; private set; }

    /// <summary>来源 IP 地址，≤64 字，可空。</summary>
    public string? IpAddress { get; private set; }

    /// <summary>操作发生时间（UTC）。</summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private OperationLog() { }

    private OperationLog(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验各字段长度与必填项，构建操作日志。操作日志仅追加，无更新方法。
    /// </summary>
    /// <param name="logId">日志标识，由应用层生成。</param>
    /// <param name="operatorId">操作运营人员标识。</param>
    /// <param name="operationType">操作类型。</param>
    /// <param name="module">所属模块。</param>
    /// <param name="description">操作描述，可空。</param>
    /// <param name="beforeSnapshot">变更前快照，可空。</param>
    /// <param name="afterSnapshot">变更后快照，可空。</param>
    /// <param name="ipAddress">来源 IP 地址，可空。</param>
    /// <param name="occurredAt">操作发生时间（UTC）。</param>
    public static OperationLog Create(
        Guid logId,
        Guid operatorId,
        string operationType,
        string module,
        string? description,
        string? beforeSnapshot,
        string? afterSnapshot,
        string? ipAddress,
        DateTime occurredAt)
    {
        if (logId == Guid.Empty)
        {
            throw new SystemAdminDomainException("日志标识不可为空", "OP_LOG_ID_EMPTY");
        }

        if (operatorId == Guid.Empty)
        {
            throw new SystemAdminDomainException("运营人员标识不可为空", "OP_LOG_OPERATOR_EMPTY");
        }

        ValidateOperationType(operationType);
        ValidateModule(module);
        ValidateDescription(description);
        ValidateSnapshot(beforeSnapshot, nameof(BeforeSnapshot));
        ValidateSnapshot(afterSnapshot, nameof(AfterSnapshot));
        ValidateIpAddress(ipAddress);

        if (occurredAt == default)
        {
            throw new SystemAdminDomainException("操作发生时间不可为空", "OP_LOG_OCCURRED_AT_EMPTY");
        }

        return new OperationLog(logId)
        {
            OperatorId = operatorId,
            OperationType = operationType.Trim(),
            Module = module.Trim(),
            Description = NormalizeNullable(description),
            BeforeSnapshot = NormalizeNullable(beforeSnapshot),
            AfterSnapshot = NormalizeNullable(afterSnapshot),
            IpAddress = NormalizeNullable(ipAddress),
            OccurredAt = occurredAt
        };
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateOperationType(string operationType)
    {
        if (string.IsNullOrWhiteSpace(operationType))
        {
            throw new SystemAdminDomainException("操作类型不可为空", "OP_LOG_TYPE_EMPTY");
        }

        if (operationType.Trim().Length > MaxOperationTypeLength)
        {
            throw new SystemAdminDomainException($"操作类型长度不可超过 {MaxOperationTypeLength} 字符", "OP_LOG_TYPE_LENGTH");
        }
    }

    private static void ValidateModule(string module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            throw new SystemAdminDomainException("模块不可为空", "OP_LOG_MODULE_EMPTY");
        }

        if (module.Trim().Length > MaxModuleLength)
        {
            throw new SystemAdminDomainException($"模块长度不可超过 {MaxModuleLength} 字符", "OP_LOG_MODULE_LENGTH");
        }
    }

    private static void ValidateDescription(string? description)
    {
        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength)
        {
            throw new SystemAdminDomainException($"操作描述长度不可超过 {MaxDescriptionLength} 字符", "OP_LOG_DESC_LENGTH");
        }
    }

    private static void ValidateSnapshot(string? snapshot, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(snapshot) && snapshot.Trim().Length > MaxSnapshotLength)
        {
            throw new SystemAdminDomainException($"{fieldName} 长度不可超过 {MaxSnapshotLength} 字符", "OP_LOG_SNAPSHOT_LENGTH");
        }
    }

    private static void ValidateIpAddress(string? ipAddress)
    {
        if (!string.IsNullOrWhiteSpace(ipAddress) && ipAddress.Trim().Length > MaxIpAddressLength)
        {
            throw new SystemAdminDomainException($"IP 地址长度不可超过 {MaxIpAddressLength} 字符", "OP_LOG_IP_LENGTH");
        }
    }
}
