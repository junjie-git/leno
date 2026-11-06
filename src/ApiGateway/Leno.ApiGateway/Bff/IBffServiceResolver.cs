namespace Leno.ApiGateway.Bff;

/// <summary>
/// BFF 下游服务寻址抽象：把清单中的服务名解析为可用的基址（<c>http://host:port</c>，无尾部斜杠）。
/// <para>
/// 解析顺序：
/// <list type="number">
///   <item><c>Bff:Services:&lt;serviceName&gt;:BaseUrl</c> 显式基址（本地开发 / K8s 等静态拓扑）；</item>
///   <item>Consul 健康实例查询（服务名取自 <c>Bff:Services:&lt;serviceName&gt;:ConsulServiceName</c>）；</item>
///   <item>两者都不可用则抛 <see cref="BffServiceUnavailableException"/>，由控制器转为统一 503 降级响应。</item>
/// </list>
/// </para>
/// </summary>
public interface IBffServiceResolver
{
    /// <summary>
    /// 解析指定服务名的基址。
    /// </summary>
    /// <param name="serviceName">清单服务名（如 <c>order</c>、<c>usercenter</c>）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>基址字符串，形如 <c>http://order-api:8080</c>，不含尾部斜杠。</returns>
    /// <exception cref="BffServiceUnavailableException">
    /// 服务未登记、基址格式非法或 Consul 无健康实例时抛出。
    /// </exception>
    Task<string> ResolveBaseAddressAsync(string serviceName, CancellationToken ct);
}
