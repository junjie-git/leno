namespace Leno.ApiGateway.Options;

/// <summary>
/// BFF 下游服务的寻址配置，对应 <c>Bff:Services:&lt;serviceName&gt;</c> 节。
/// <para>
/// 服务名（配置键）必须与 <c>deploy/services.manifest.json</c> 中 <c>services[].name</c> 保持一致，
/// 例如 <c>order</c>、<c>usercenter</c>、<c>sellershop</c>；Consul 注册名则由清单的
/// <c>composeService</c> 加 <c>leno-</c> 前缀得到（如 <c>leno-user-center-api</c>）。
/// </para>
/// </summary>
public sealed class BffServiceEndpoint
{
    /// <summary>
    /// 显式基址（形如 <c>http://localhost:5154</c>，不含尾部斜杠）。
    /// <para>
    /// 用于本地开发（各服务跑在 localhost 的清单端口）以及 K8s 等无法访问 Consul 的场景。
    /// 配置后优先于 Consul 服务发现。
    /// </para>
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Consul 中注册的服务名（形如 <c>leno-order-api</c>）。
    /// <para>
    /// 未显式配置 <see cref="BaseUrl"/> 时，通过 Consul 健康实例查询得到真实的 host:port。
    /// 该值必须与服务启动时 <c>AddConsulServiceRegistration(...)</c> 传入的名字一致。
    /// </para>
    /// </summary>
    public string? ConsulServiceName { get; set; }
}
