namespace Leno.ApiGateway.Bff.Models;

/// <summary>
/// BFF 聚合响应封装。
/// <para>
/// 即使部分下游失败（<see cref="Partial"/>=true），HTTP 状态码仍为 200，
/// 客户端通过 <see cref="Errors"/> 字段获取失败明细，通过 <see cref="Data"/> 获取已成功的部分数据。
/// </para>
/// </summary>
public sealed class BffResponse<T>
{
    /// <summary>全部下游均成功时为 true。</summary>
    public bool Success { get; init; }

    /// <summary>部分下游成功、部分失败时为 true。</summary>
    public bool Partial { get; init; }

    /// <summary>聚合后的响应数据。即使 Partial=true 也可能含部分有效数据。</summary>
    public T? Data { get; init; }

    /// <summary>失败的下游错误明细。全部成功时为空数组。</summary>
    public IReadOnlyList<BffError> Errors { get; init; } = Array.Empty<BffError>();
}

/// <summary>
/// 单个下游调用的错误明细。
/// </summary>
public sealed class BffError
{
    /// <summary>下游服务名（用于错误定位，对应 <see cref="BffDownstreamRequest.Source"/>）。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>HTTP 状态码；超时为 504，其他传输异常为 500。</summary>
    public int StatusCode { get; init; }

    /// <summary>错误消息（下游响应体摘要或异常 Message）。</summary>
    public string Message { get; init; } = string.Empty;
}
