using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Leno.ApiGateway.Bff.Models;

namespace Leno.ApiGateway.Bff.Dag;

/// <summary>
/// BFF DAG 节点工厂：将 <see cref="BffDownstreamRequest"/> 包装为 <see cref="AggregateNode"/>，
/// 复用 <see cref="BffForwarderService"/> 的 HTTP 调用逻辑，下游非 2xx 时抛 <see cref="DagNodeException"/>。
/// <para>
/// 典型用法：
/// <code>
/// var graph = new AggregateBuilder()
///     .AddNode(_factory.CreateNode("order", orderRequest, requestId))
///     .AddNode(_factory.CreateNode("logistics", logisticsRequest, requestId))
///     .DependsOn("logistics", "order")
///     .Build();
/// var result = await _forwarder.ExecuteDagAsync(graph, ct);
/// </code>
/// </para>
/// </summary>
public sealed class BffDagNodeFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly TimeSpan DefaultNodeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>构造 BFF DAG 节点工厂。</summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂（复用 "BffForwarder" 命名客户端）。</param>
    public BffDagNodeFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <summary>
    /// 将下游 HTTP 请求包装为 DAG 节点。
    /// </summary>
    /// <param name="name">节点名（同一图内唯一）。</param>
    /// <param name="request">下游 HTTP 请求描述。</param>
    /// <param name="requestId">请求追踪标识（透传到 X-Request-Id 头）。</param>
    /// <param name="timeout">节点超时，默认 5 秒。</param>
    /// <returns>可加入 <see cref="AggregateBuilder"/> 的 <see cref="AggregateNode"/>。</returns>
    public AggregateNode CreateNode(
        string name,
        BffDownstreamRequest request,
        string requestId,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new AggregateNode(name, async (ctx, ct) =>
        {
            try
            {
                return await BffForwarderService.SendDownstreamRequestAsync(
                    _httpClientFactory, request, requestId, ct).ConfigureAwait(false);
            }
            catch (BffForwarderService.DownstreamFailureException ex)
            {
                throw new DagNodeException(name, ex.StatusCode, ex.Message, ex);
            }
        }, timeout ?? DefaultNodeTimeout);
    }

    /// <summary>
    /// 将下游 HTTP 请求包装为 DAG 节点，允许根据上游结果动态构造请求 URL/Body。
    /// </summary>
    /// <param name="name">节点名。</param>
    /// <param name="requestBuilder">
    /// 接收已完成上游结果字典，返回 <see cref="BffDownstreamRequest"/>。
    /// 返回 null 表示跳过该节点（结果为 null）。
    /// </param>
    /// <param name="requestId">请求追踪标识。</param>
    /// <param name="timeout">节点超时。</param>
    /// <returns>DAG 节点。</returns>
    public AggregateNode CreateNode(
        string name,
        Func<IReadOnlyDictionary<string, object?>, BffDownstreamRequest?> requestBuilder,
        string requestId,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(requestBuilder);
        return new AggregateNode(name, async (ctx, ct) =>
        {
            var request = requestBuilder(ctx);
            if (request is null)
            {
                return null;
            }
            try
            {
                return await BffForwarderService.SendDownstreamRequestAsync(
                    _httpClientFactory, request, requestId, ct).ConfigureAwait(false);
            }
            catch (BffForwarderService.DownstreamFailureException ex)
            {
                throw new DagNodeException(name, ex.StatusCode, ex.Message, ex);
            }
        }, timeout ?? DefaultNodeTimeout);
    }
}
