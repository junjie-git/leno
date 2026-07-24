namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// 拓扑排序器：基于 Kahn 算法对 <see cref="AggregateNode"/> 集合排序，并检测环。
/// <para>
/// 算法步骤：
/// <list type="number">
///   <item>计算每个节点的入度（被依赖次数）</item>
///   <item>将入度为 0 的节点入队</item>
///   <item>每次出队一个节点，将其下游节点入度减 1，若变为 0 则入队</item>
///   <item>若最终排序结果数 &lt; 节点总数，说明存在环</item>
/// </list>
/// </para>
/// </summary>
public static class TopologicalSorter
{
    /// <summary>
    /// 对节点集合执行拓扑排序。
    /// </summary>
    /// <param name="nodes">待排序的节点集合。</param>
    /// <returns>按执行顺序排列的节点列表（依赖在前，被依赖在后）。</returns>
    /// <exception cref="InvalidOperationException">图中存在环或缺失依赖。</exception>
    public static IReadOnlyList<AggregateNode> Sort(IEnumerable<AggregateNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var nodeMap = nodes.ToDictionary(n => n.Name, n => n, StringComparer.Ordinal);
        if (nodeMap.Count == 0)
        {
            return Array.Empty<AggregateNode>();
        }

        // 校验依赖存在性
        foreach (var node in nodeMap.Values)
        {
            foreach (var dep in node.Dependencies)
            {
                if (!nodeMap.ContainsKey(dep))
                {
                    throw new InvalidOperationException(
                        $"节点 '{node.Name}' 声明依赖 '{dep}'，但该依赖在图中不存在。");
                }
            }
        }

        // 计算入度：每个节点的依赖数（被多少上游约束）
        var inDegree = nodeMap.Values.ToDictionary(n => n.Name, _ => 0, StringComparer.Ordinal);
        var dependents = nodeMap.Values.ToDictionary(n => n.Name, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var node in nodeMap.Values)
        {
            inDegree[node.Name] = node.Dependencies.Count;
            foreach (var dep in node.Dependencies)
            {
                dependents[dep].Add(node.Name);
            }
        }

        // 入度为 0 的节点入队
        var queue = new Queue<string>();
        foreach (var kv in inDegree)
        {
            if (kv.Value == 0)
            {
                queue.Enqueue(kv.Key);
            }
        }

        var sorted = new List<AggregateNode>(nodeMap.Count);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(nodeMap[current]);
            foreach (var downstream in dependents[current])
            {
                inDegree[downstream]--;
                if (inDegree[downstream] == 0)
                {
                    queue.Enqueue(downstream);
                }
            }
        }

        if (sorted.Count != nodeMap.Count)
        {
            var cyclic = nodeMap.Keys.Except(sorted.Select(n => n.Name), StringComparer.Ordinal).ToList();
            throw new InvalidOperationException(
                $"聚合图存在环依赖，无法拓扑排序。涉及节点：{string.Join(", ", cyclic)}");
        }

        return sorted;
    }
}
