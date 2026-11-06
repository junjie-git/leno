using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Leno.Infrastructure.HealthChecks;

/// <summary>
/// RabbitMQ 就绪探针（直接基于 RabbitMQ.Client v7 的 <see cref="IConnectionFactory"/>）。
/// <para>
/// 背景：<c>AspNetCore.HealthChecks.RabbitMQ 8.0.x</c> 仍引用 v6 的 <c>RabbitMQ.Client.IModel</c>，
/// 与工程实际解析到的 RabbitMQ.Client 7.0.0 不兼容，探针执行时抛
/// <c>Could not load type 'RabbitMQ.Client.IModel'</c>，导致 <c>/health/ready</c> 恒 503。
/// </para>
/// <para>
/// 本实现每次探针执行时按配置建连并开一个信道，校验通道可用后立即释放（<c>await using</c>），
/// 不依赖外部健康检查包，也不会泄漏连接。
/// </para>
/// </summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    /// <summary>默认 AMQP 端口。</summary>
    private const int DefaultPort = 5672;

    /// <summary>默认 AMQP 虚拟主机。</summary>
    private const string DefaultVirtualHost = "/";

    /// <summary>建连超时上限，避免探针拖到健康检查整体超时。</summary>
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(10);

    private readonly string _host;
    private readonly int _port;
    private readonly string _userName;
    private readonly string _password;
    private readonly string _virtualHost;

    /// <summary>
    /// 构造函数，从配置读取 RabbitMQ 连接参数（键与 <c>AddLenoInfrastructure</c> 保持一致）。
    /// </summary>
    /// <param name="configuration">应用配置。</param>
    public RabbitMqHealthCheck(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _host = configuration["RabbitMQ:Host"] ?? string.Empty;

        var portText = configuration["RabbitMQ:Port"];
        _port = int.TryParse(portText, out var parsedPort) ? parsedPort : DefaultPort;

        _userName = configuration["RabbitMQ:Username"] ?? "guest";
        _password = configuration["RabbitMQ:Password"] ?? "guest";

        var virtualHost = configuration["RabbitMQ:VirtualHost"];
        _virtualHost = string.IsNullOrWhiteSpace(virtualHost) ? DefaultVirtualHost : virtualHost;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(_host))
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "RabbitMQ:Host 未配置，无法执行就绪探测。");
        }

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _host,
                Port = _port,
                UserName = _userName,
                Password = _password,
                VirtualHost = _virtualHost,
                RequestedConnectionTimeout = ConnectionTimeout
            };

            await using var connection = await factory
                .CreateConnectionAsync(cancellationToken)
                .ConfigureAwait(false);

            await using var channel = await connection
                .CreateChannelAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy($"RabbitMQ 就绪（{_host}:{_port}，虚拟主机 {_virtualHost}）。");
        }
        catch (Exception ex)
        {
            // 健康检查属于边界：任何连接/协议异常都必须转为 Unhealthy 结果，不能向上抛。
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"RabbitMQ 不可用（{_host}:{_port}）：{ex.Message}",
                ex);
        }
    }
}
