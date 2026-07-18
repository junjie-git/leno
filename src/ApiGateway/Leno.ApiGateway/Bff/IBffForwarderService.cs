using Leno.ApiGateway.Bff.Models;

namespace Leno.ApiGateway.Bff;

/// <summary>
/// BFF 聚合转发服务：并行调用多个下游服务，3 秒超时，部分失败返回 Partial=true + 错误明细。
/// </summary>
public interface IBffForwarderService
{
    /// <summary>
    /// 并行调用多个下游服务并聚合响应。
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
