namespace Leno.ApiGateway.Bff;

/// <summary>
/// BFF 下游服务无法寻址时抛出的异常（未登记、基址非法、Consul 无健康实例）。
/// <para>
/// 控制器捕获后返回与网关一致的 503 降级信封（<c>{ code, message, data }</c>），
/// 不得吞掉异常返回空对象。
/// </para>
/// </summary>
public sealed class BffServiceUnavailableException : Exception
{
    /// <summary>构造下游不可用异常。</summary>
    /// <param name="serviceName">清单服务名。</param>
    /// <param name="consulServiceName">尝试查询的 Consul 服务名（未配置时为空字符串）。</param>
    /// <param name="message">面向运维的失败原因。</param>
    /// <param name="innerException">内部异常（如地址格式错误）。</param>
    public BffServiceUnavailableException(
        string serviceName,
        string consulServiceName,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ServiceName = serviceName;
        ConsulServiceName = consulServiceName;
    }

    /// <summary>清单服务名。</summary>
    public string ServiceName { get; }

    /// <summary>尝试查询的 Consul 服务名；未配置时为空字符串。</summary>
    public string ConsulServiceName { get; }
}
