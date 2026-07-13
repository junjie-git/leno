namespace Leno.ApiGateway.Options;

/// <summary>
/// 网关顶层配置节，对应 appsettings.json 中 <c>Consul</c> 节。
/// </summary>
public sealed class ConsulOptions
{
    /// <summary>Consul Agent HTTP 地址。</summary>
    public string Url { get; set; } = "http://localhost:8500";

    /// <summary>Consul ACL Token（可选）。</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>健康实例查询时是否仅返回 passing 状态的实例。</summary>
    public bool PassingOnly { get; set; } = true;
}
