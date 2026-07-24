using System.Text.Json;

namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// DAG 聚合图中的一个节点：携带执行委托、超时与依赖列表。
/// <para>
/// 节点的 <see cref="Executor"/> 接收已完成节点结果字典（以节点名为键，<see cref="object"/>? 为值），
/// 返回 <see cref="object"/>?（通常为 <see cref="JsonElement"/>? 或已反序列化的 DTO）。
/// </para>
/// </summary>
public sealed class AggregateNode
{
    /// <summary>节点唯一标识（在同一 <see cref="AggregateGraph"/> 内必须唯一）。</summary>
    public string Name { get; }

    /// <summary>
    /// 节点执行委托：接收已完成上游节点的结果快照与取消令牌，返回该节点的结果。
    /// 失败时抛异常，由 <see cref="DagOrchestrator"/> 捕获并转为 <see cref="Bff.Models.BffError"/>。
    /// </summary>
    public Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<object?>> Executor { get; }

    /// <summary>单个节点的超时阈值。超时后该节点及其下游节点被级联取消。</summary>
    public TimeSpan Timeout { get; }

    /// <summary>该节点依赖的上游节点名集合（必须全部完成后本节点才可执行）。</summary>
    public HashSet<string> Dependencies { get; } = new(StringComparer.Ordinal);

    /// <summary>构造节点。</summary>
    /// <param name="name">节点唯一标识。</param>
    /// <param name="executor">执行委托。</param>
    /// <param name="timeout">节点超时，必须为正值。</param>
    public AggregateNode(
        string name,
        Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<object?>> executor,
        TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("节点名不能为空或空白", nameof(name));
        }
        Name = name;
        Executor = executor ?? throw new ArgumentNullException(nameof(executor));
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "节点超时必须为正值");
        }
        Timeout = timeout;
    }
}
