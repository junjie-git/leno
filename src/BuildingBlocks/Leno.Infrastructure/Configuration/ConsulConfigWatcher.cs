using System.Text;
using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.Configuration;

/// <summary>
/// Consul KV 配置热更新后台服务（M4 双轨方案）。
/// 长轮询 <c>leno/anticorruption/use-grpc/{bc}</c> KV，1-2 秒内生效。
/// 5 分钟超时阻塞（Consul 长轮询机制），异常重试 10 秒间隔。
/// 配合 <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> 实现配置热更新到 AntiCorruptionDispatcher。
/// </summary>
public sealed class ConsulConfigWatcher : BackgroundService
{
    private const string UseGrpcKeyPrefix = "leno/anticorruption/use-grpc/";
    private static readonly TimeSpan WaitTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private readonly IConsulClient _consul;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConsulConfigWatcher> _logger;
    private readonly string _bcName;
    private readonly string _useGrpcKey;

    public ConsulConfigWatcher(
        IConsulClient consul,
        IConfiguration configuration,
        ILogger<ConsulConfigWatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(consul);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _consul = consul;
        _configuration = configuration;
        _logger = logger;
        _bcName = configuration["Service:Name"] ?? string.Empty;
        _useGrpcKey = UseGrpcKeyPrefix + _bcName;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_bcName))
        {
            _logger.LogWarning("Service:Name 未配置，ConsulConfigWatcher 退出");
            return;
        }

        _logger.LogInformation("ConsulConfigWatcher 启动，监听 KV: {Key}", _useGrpcKey);

        ulong? waitIndex = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var queryResult = await _consul.KV.Get(_useGrpcKey, new QueryOptions
                {
                    WaitIndex = waitIndex ?? 0,
                    WaitTime = WaitTime
                }, ct).ConfigureAwait(false);

                if (queryResult.Response is not null && queryResult.LastIndex != waitIndex)
                {
                    waitIndex = queryResult.LastIndex;
                    var newValue = Encoding.UTF8.GetString(queryResult.Response.Value);
                    _configuration["AntiCorruption:UseGrpc"] = newValue;
                    _logger.LogInformation("UseGrpc 配置热更新为 {Value}（BC={BC}）", newValue, _bcName);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Consul KV watch 失败，{Seconds} 秒后重试", RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("ConsulConfigWatcher 退出");
    }
}
