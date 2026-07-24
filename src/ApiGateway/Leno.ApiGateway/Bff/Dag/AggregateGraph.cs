namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// 不可变的聚合 DAG 图：持有节点字典与拓扑排序结果。
/// 由 <see cref="AggregateBuilder.Build"/> 构造，交由 <see cref="DagOrchestrator"/> 执行。
/// </summary>
public sealed class AggregateGraph
{
    /// <summary>图中所有节点（按名索引）。</summary>
    public IReadOnlyDictionary<string, AggregateNode> Nodes { get; }

    /// <summary>拓扑排序后的节点列表（依赖在前）。</summary>
    public IReadOnlyList<AggregateNode> SortedNodes { get; }

    /// <summary>图中节点总数。</summary>
    public int Count => Nodes.Count;

    internal AggregateGraph(
        IReadOnlyDictionary<string, AggregateNode> nodes,
        IReadOnlyList<AggregateNode> sortedNodes)
    {
        Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        SortedNodes = sortedNodes ?? throw new ArgumentNullException(nameof(sortedNodes));
    }
}
