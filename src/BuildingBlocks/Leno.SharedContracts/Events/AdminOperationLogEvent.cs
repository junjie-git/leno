using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 运营操作日志事件，由各业务域在运营操作后发布，供系统管理域消费并持久化操作日志。
/// </summary>
public sealed class AdminOperationLogEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>操作人员标识。</summary>
    public Guid OperatorId { get; init; }

    /// <summary>操作类型。</summary>
    public string OperationType { get; init; } = string.Empty;

    /// <summary>所属模块。</summary>
    public string Module { get; init; } = string.Empty;

    /// <summary>操作描述。</summary>
    public string? Description { get; init; }

    /// <summary>来源 IP 地址。</summary>
    public string? IpAddress { get; init; }

    /// <summary>聚合根标识，用于发件箱归类，映射至操作涉及的聚合根。</summary>
    public Guid AggregateId { get; init; }

    public AdminOperationLogEvent() : base() { }

    public AdminOperationLogEvent(
        Guid operatorId,
        string operationType,
        string module,
        string? description,
        string? ipAddress,
        Guid aggregateId) : base()
    {
        OperatorId = operatorId;
        OperationType = operationType ?? string.Empty;
        Module = module ?? string.Empty;
        Description = description;
        IpAddress = ipAddress;
        AggregateId = aggregateId;
    }
}