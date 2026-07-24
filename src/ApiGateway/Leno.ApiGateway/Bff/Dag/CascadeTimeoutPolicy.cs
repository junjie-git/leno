using System.Collections.Concurrent;
using Leno.ApiGateway.Bff.Models;

namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// 级联超时策略：记录超时节点，提供 <see cref="IsCancelled"/> 查询供 <see cref="DagOrchestrator"/>
/// 在调度就绪节点前判断其上游是否已被级联取消。
/// <para>
/// 当某节点超时，其所有下游节点（直接或间接依赖它）都应被跳过并标记为失败，
/// 避免下游节点因缺少上游数据而执行无意义调用。
/// </para>
/// </summary>
public sealed class CascadeTimeoutPolicy
{
    private readonly ConcurrentDictionary<string, byte> _cancelledNodes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _cancelledAt = new(StringComparer.Ordinal);
    private int _totalTimeouts;

    /// <summary>构造级联超时策略。</summary>
    public CascadeTimeoutPolicy()
    {
    }

    /// <summary>
    /// 记录节点超时，将其加入级联取消集合。
    /// </summary>
    /// <param name="nodeName">超时节点名。</param>
    public void OnNodeTimeout(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return;
        }
        if (_cancelledNodes.TryAdd(nodeName, 0))
        {
            _cancelledAt.TryAdd(nodeName, DateTime.UtcNow);
            Interlocked.Increment(ref _totalTimeouts);
        }
    }

    /// <summary>
    /// 查询指定节点是否已被级联取消（超时）。
    /// </summary>
    /// <param name="nodeName">节点名。</param>
    /// <returns>已超时则 true。</returns>
    public bool IsCancelled(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return false;
        }
        return _cancelledNodes.ContainsKey(nodeName);
    }

    /// <summary>
    /// 重置策略状态（用于单次执行的策略实例复用场景）。
    /// </summary>
    public void Reset()
    {
        _cancelledNodes.Clear();
        _cancelledAt.Clear();
        Interlocked.Exchange(ref _totalTimeouts, 0);
    }

    /// <summary>累计超时节点总数。</summary>
    public int TotalTimeouts => _totalTimeouts;

    /// <summary>当前已被级联取消的节点名快照。</summary>
    public IReadOnlyCollection<string> CancelledNodes => _cancelledNodes.Keys.ToArray();
}
