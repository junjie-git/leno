namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// DAG 节点执行异常：携带 HTTP 状态码，供 <see cref="DagOrchestrator"/> 在 catch 时
/// 保留原始下游状态码（如 404/503），而非统一标记为 500。
/// <para>
/// 节点 <see cref="AggregateNode.Executor"/> 在下游返回非 2xx 时应抛此异常；
/// 其他异常由 <see cref="DagOrchestrator"/> 统一标记为 500。
/// </para>
/// </summary>
public sealed class DagNodeException : Exception
{
    /// <summary>下游 HTTP 状态码（默认 500）。</summary>
    public int StatusCode { get; }

    /// <summary>节点名（错误来源标识）。</summary>
    public string NodeName { get; }

    /// <summary>构造 DAG 节点执行异常。</summary>
    /// <param name="nodeName">节点名。</param>
    /// <param name="statusCode">HTTP 状态码。</param>
    /// <param name="message">错误消息。</param>
    /// <param name="inner">内部异常。</param>
    public DagNodeException(string nodeName, int statusCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        NodeName = nodeName;
        StatusCode = statusCode;
    }
}
