namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// 声明式聚合构建器：通过 <see cref="AddNode"/> 声明节点 + <see cref="DependsOn"/> 描述依赖关系，
/// <see cref="Build"/> 时执行拓扑排序校验无环，返回不可变的 <see cref="AggregateGraph"/>。
/// <para>
/// 典型用法：
/// <code>
/// var graph = new AggregateBuilder()
///     .AddNode("user", async (ctx, ct) => await GetUserAsync(userId, ct))
///     .AddNode("order", async (ctx, ct) => await GetOrderAsync(orderId, ctx["user"], ct), TimeSpan.FromSeconds(2))
///     .DependsOn("order", "user")
///     .AddNode("items", async (ctx, ct) => await GetItemsAsync(ctx["order"], ct))
///     .DependsOn("items", "order")
///     .Build();
/// </code>
/// </para>
/// </summary>
public sealed class AggregateBuilder
{
    private readonly Dictionary<string, AggregateNode> _nodes = new(StringComparer.Ordinal);
    private static readonly TimeSpan DefaultNodeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 添加一个聚合节点。
    /// </summary>
    /// <param name="name">节点唯一标识（在同一构建器内必须唯一）。</param>
    /// <param name="executor">节点执行委托，接收已完成上游结果快照与取消令牌。</param>
    /// <param name="timeout">节点超时，默认 5 秒。</param>
    /// <returns>当前构建器（链式调用）。</returns>
    public AggregateBuilder AddNode(
        string name,
        Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<object?>> executor,
        TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("节点名不能为空或空白", nameof(name));
        }
        if (_nodes.ContainsKey(name))
        {
            throw new InvalidOperationException($"节点 '{name}' 在聚合图中已存在。");
        }
        _nodes[name] = new AggregateNode(name, executor, timeout ?? DefaultNodeTimeout);
        return this;
    }

    /// <summary>
    /// 添加一个已构造的 <see cref="AggregateNode"/>（如通过 <see cref="BffDagNodeFactory"/> 创建的节点）。
    /// </summary>
    /// <param name="node">已构造的聚合节点，其 <see cref="AggregateNode.Name"/> 必须在同一构建器内唯一。</param>
    /// <returns>当前构建器（链式调用）。</returns>
    public AggregateBuilder AddNode(AggregateNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_nodes.ContainsKey(node.Name))
        {
            throw new InvalidOperationException($"节点 '{node.Name}' 在聚合图中已存在。");
        }
        _nodes[node.Name] = node;
        return this;
    }

    /// <summary>
    /// 声明 <paramref name="dependent"/> 依赖于 <paramref name="dependencies"/>。
    /// <para>
    /// dependent 必须在 dependencies 全部完成后才可执行。
    /// </para>
    /// </summary>
    /// <param name="dependent">依赖方节点名（必须已通过 <see cref="AddNode"/> 添加）。</param>
    /// <param name="dependencies">被依赖的节点名列表（必须已通过 <see cref="AddNode"/> 添加）。</param>
    /// <returns>当前构建器（链式调用）。</returns>
    public AggregateBuilder DependsOn(string dependent, params string[] dependencies)
    {
        if (string.IsNullOrWhiteSpace(dependent))
        {
            throw new ArgumentException("依赖方节点名不能为空", nameof(dependent));
        }
        ArgumentNullException.ThrowIfNull(dependencies);
        if (!_nodes.TryGetValue(dependent, out var node))
        {
            throw new InvalidOperationException($"节点 '{dependent}' 不存在，无法声明依赖。");
        }
        foreach (var dep in dependencies)
        {
            if (string.IsNullOrWhiteSpace(dep))
            {
                throw new ArgumentException("依赖节点名不能为空或空白", nameof(dependencies));
            }
            if (string.Equals(dep, dependent, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"节点 '{dependent}' 不能依赖自身。");
            }
            if (!_nodes.ContainsKey(dep))
            {
                throw new InvalidOperationException($"依赖 '{dep}' 不存在，无法为节点 '{dependent}' 声明依赖。");
            }
            node.Dependencies.Add(dep);
        }
        return this;
    }

    /// <summary>
    /// 构建不可变的 <see cref="AggregateGraph"/>，内部执行拓扑排序校验无环。
    /// </summary>
    /// <returns>已校验的聚合图。</returns>
    public AggregateGraph Build()
    {
        var sorted = TopologicalSorter.Sort(_nodes.Values);
        return new AggregateGraph(_nodes, sorted);
    }
}
