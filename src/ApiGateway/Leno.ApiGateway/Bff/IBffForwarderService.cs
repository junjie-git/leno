using Leno.ApiGateway.Bff.Dag;
using Leno.ApiGateway.Bff.Models;

namespace Leno.ApiGateway.Bff;

/// <summary>
/// BFF 聚合转发服务：支持两种聚合模式。
/// <list type="bullet">
///   <item>
///     <see cref="ForwardAsync{T}"/>：基于 <see cref="Parallel.ForEachAsync"/> 的无依赖并行聚合（特例保留）。
///     适用于所有下游请求相互独立、可一次性并行调用的场景。
///   </item>
///   <item>
///     <see cref="ExecuteDagAsync"/>：基于 <see cref="IDagOrchestrator"/> 的 DAG 编排引擎。
///     适用于下游请求存在依赖链（如先查用户再查用户订单）的场景，自动拓扑排序 + 分波并行 + 级联超时。
///   </item>
/// </list>
/// </summary>
public interface IBffForwarderService
{
    /// <summary>
    /// 并行调用多个下游服务并聚合响应（无依赖场景特例，基于 <see cref="Parallel.ForEachAsync"/>）。
    /// </summary>
    /// <typeparam name="T">聚合后的响应 DTO 类型。</typeparam>
    /// <param name="requestId">请求追踪标识（透传到下游 X-Request-Id 头）。</param>
    /// <param name="requests">下游请求列表（每个请求含 Source 标识与 ServiceUrl）。</param>
    /// <param name="aggregator">
    /// 聚合函数：接收以 <see cref="BffDownstreamRequest.Source"/> 为键、<see cref="System.Text.Json.JsonElement"/>?（失败时为 null）为值的字典，
    /// 返回聚合后的 <typeparamref name="T"/> 实例。
    /// </param>
    /// <param name="ct">调用方取消令牌。</param>
    /// <returns>
    /// <see cref="BffResponse{T}"/>：
    /// <list type="bullet">
    ///   <item>全部成功：Success=true、Partial=false、Errors=空</item>
    ///   <item>部分失败：Success=false、Partial=true、Errors 含失败明细</item>
    ///   <item>全部失败：Success=false、Partial=false、Errors 含全部失败明细</item>
    /// </list>
    /// </returns>
    Task<BffResponse<T>> ForwardAsync<T>(
        string requestId,
        IReadOnlyList<BffDownstreamRequest> requests,
        Func<IReadOnlyDictionary<string, System.Text.Json.JsonElement?>, T> aggregator,
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// 通过 DAG 编排引擎执行聚合图：自动拓扑排序、分波并行调度、节点超时级联取消。
    /// <para>
    /// 适用于下游请求存在依赖链的场景（如 user → order → items → snapshot）。
    /// 无依赖场景也可使用此方法（所有节点第一波就绪，等价于 <see cref="ForwardAsync{T}"/>）。
    /// </para>
    /// </summary>
    /// <param name="graph">通过 <see cref="AggregateBuilder.Build"/> 构建的聚合图。</param>
    /// <param name="ct">调用方取消令牌。</param>
    /// <returns>
    /// <see cref="AggregateResult"/>：包含已完成节点结果字典与失败明细。
    /// 调用方可通过 <see cref="AggregateResult.GetResult{T}(string)"/> 或直接访问 <see cref="AggregateResult.Results"/> 获取节点结果。
    /// </returns>
    Task<AggregateResult> ExecuteDagAsync(AggregateGraph graph, CancellationToken ct = default);
}

/// <summary>
/// BFF 下游调用请求描述。
/// </summary>
public sealed class BffDownstreamRequest
{
    /// <summary>下游服务名（用于错误定位与结果聚合键，需在同一调用内唯一）。</summary>
    public required string Source { get; init; }

    /// <summary>下游服务完整 URL（含 scheme/host/path/query）。</summary>
    public required string ServiceUrl { get; init; }

    /// <summary>HTTP 方法，默认 GET。</summary>
    public string Method { get; init; } = "GET";

    /// <summary>请求体 JSON（POST/PUT/PATCH 时使用，GET 忽略）。</summary>
    public string? RequestBody { get; init; }
}
