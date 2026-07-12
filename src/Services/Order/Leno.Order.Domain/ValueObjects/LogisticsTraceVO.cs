namespace Leno.Order.Domain.ValueObjects;

/// <summary>
/// 物流轨迹查询结果值对象，封装物流轨迹节点列表与缓存来源标识。
/// </summary>
public sealed class LogisticsTraceResult
{
    /// <summary>物流单号。</summary>
    public string LogisticsNo { get; }

    /// <summary>物流公司编码。</summary>
    public string CompanyCode { get; }

    /// <summary>物流轨迹节点列表。</summary>
    public IReadOnlyList<LogisticsTraceNode> Nodes { get; }

    /// <summary>是否来自缓存。</summary>
    public bool IsFromCache { get; }

    /// <summary>
    /// 构造物流轨迹查询结果。
    /// </summary>
    /// <param name="logisticsNo">物流单号。</param>
    /// <param name="companyCode">物流公司编码。</param>
    /// <param name="nodes">轨迹节点列表。</param>
    /// <param name="isFromCache">是否来自缓存。</param>
    public LogisticsTraceResult(string logisticsNo, string companyCode, IEnumerable<LogisticsTraceNode> nodes, bool isFromCache = false)
    {
        LogisticsNo = logisticsNo ?? string.Empty;
        CompanyCode = companyCode ?? string.Empty;
        Nodes = (nodes ?? Array.Empty<LogisticsTraceNode>()).ToList().AsReadOnly();
        IsFromCache = isFromCache;
    }

    /// <summary>
    /// 创建空结果（物流单号无轨迹数据）。
    /// </summary>
    /// <param name="logisticsNo">物流单号。</param>
    /// <param name="companyCode">物流公司编码。</param>
    public static LogisticsTraceResult Empty(string logisticsNo, string companyCode)
        => new(logisticsNo, companyCode, Array.Empty<LogisticsTraceNode>(), false);

    /// <summary>
    /// 创建来自缓存的空结果。
    /// </summary>
    /// <param name="logisticsNo">物流单号。</param>
    /// <param name="companyCode">物流公司编码。</param>
    public static LogisticsTraceResult EmptyFromCache(string logisticsNo, string companyCode)
        => new(logisticsNo, companyCode, Array.Empty<LogisticsTraceNode>(), true);
}

/// <summary>
/// 物流轨迹节点值对象，表示物流轨迹中的一个节点。
/// </summary>
public sealed class LogisticsTraceNode
{
    /// <summary>轨迹描述。</summary>
    public string Description { get; }

    /// <summary>发生时间（UTC）。</summary>
    public DateTime OccurredAt { get; }

    /// <summary>发生地点。</summary>
    public string Location { get; }

    /// <summary>
    /// 构造物流轨迹节点。
    /// </summary>
    /// <param name="description">轨迹描述。</param>
    /// <param name="occurredAt">发生时间（UTC）。</param>
    /// <param name="location">发生地点。</param>
    public LogisticsTraceNode(string description, DateTime occurredAt, string location)
    {
        Description = description ?? string.Empty;
        OccurredAt = occurredAt;
        Location = location ?? string.Empty;
    }
}