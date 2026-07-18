namespace Leno.Order.Application.Queries;

/// <summary>
/// 物流轨迹查询结果（CQRS 读侧 Query Result）。
/// 由 <see cref="LogisticsTraceQueryHandler"/> 加载订单聚合后调用 <c>ILogisticsTrackingService</c> 获取实时轨迹。
/// </summary>
public sealed class LogisticsTraceResult
{
    public Guid OrderId { get; init; }

    /// <summary>物流单号，订单未发货时为 null。</summary>
    public string? TrackingNo { get; init; }

    /// <summary>物流公司编码，订单未发货或未配置物流公司时为 null。</summary>
    public string? LogisticsCompany { get; init; }

    /// <summary>物流轨迹节点列表，按时间倒序返回。无轨迹时为空列表。</summary>
    public IReadOnlyList<LogisticsTraceNode> Nodes { get; init; } = Array.Empty<LogisticsTraceNode>();
}

/// <summary>
/// 物流轨迹节点 DTO，表示物流轨迹中的一个节点。
/// </summary>
public sealed class LogisticsTraceNode
{
    /// <summary>发生时间（UTC）。</summary>
    public DateTime Time { get; init; }

    /// <summary>轨迹描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>发生地点，可为空。</summary>
    public string? Location { get; init; }
}
