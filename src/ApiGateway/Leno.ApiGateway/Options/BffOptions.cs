namespace Leno.ApiGateway.Options;

/// <summary>
/// BFF 聚合转发配置，对应 appsettings.json 中 <c>Bff</c> 节。
/// <para>
/// T15 修复：区分整体超时与单请求超时。
/// 整体超时（<see cref="OverallTimeout"/>）应大于单请求超时（<see cref="PerRequestTimeout"/>），
/// 否则单请求超时无意义（整体先于单请求触发）。
/// </para>
/// </summary>
public sealed class BffOptions
{
    /// <summary>
    /// 整体超时：BFF 聚合所有下游请求的总超时，默认 10 秒。
    /// 超时后未完成的请求标记为 504。
    /// </summary>
    public TimeSpan OverallTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 单请求超时：单个下游请求的超时，默认 3 秒。
    /// 超时仅影响该请求（标记 504），不影响其他并行请求。
    /// </summary>
    public TimeSpan PerRequestTimeout { get; set; } = TimeSpan.FromSeconds(3);
}
