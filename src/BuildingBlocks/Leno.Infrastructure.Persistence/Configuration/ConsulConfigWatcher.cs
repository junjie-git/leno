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
/// <remarks>
/// T19 修复：通过 <see cref="ConsulReloadableConfigurationProvider"/> 写入配置值并触发 <c>OnReload</c>，
/// 使 <c>IOptionsMonitor&lt;AntiCorruptionOptions&gt;</c> 感知 KV 变更并重新绑定 CurrentValue。
/// 不再直接 <c>_configuration["..."] = value</c>（不触发 IOptionsMonitor 重载）。
/// </remarks>
public sealed class ConsulConfigWatcher : BackgroundService
{
    private const string UseGrpcKeyPrefix = "leno/anticorruption/use-grpc/";
    private const string UseGrpcConfigKey = "AntiCorruption:UseGrpc";
    /// <summary>Schema 版本校验 key 前缀（阶段四 4.6 步骤6）。</summary>
    private const string ConfigKeyPrefix = "leno/config/";
    private const string GrayPercentKeySuffix = "/gray-percent";
    private static readonly TimeSpan WaitTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private readonly IConsulClient _consul;
    private readonly IConfiguration _configuration;
    private readonly ConsulReloadableConfigurationProvider? _consulProvider;
    private readonly ILogger<ConsulConfigWatcher> _logger;
    private readonly string _bcName;
    private readonly string _useGrpcKey;
    /// <summary>Schema 版本校验器（阶段四 4.6 步骤6，可为 null 表示不启用版本校验）。</summary>
    private readonly ConsulConfigSchemaValidator? _schemaValidator;
    /// <summary>灰度发布服务（阶段四 4.6 步骤6，可为 null 表示不启用灰度判定）。</summary>
    private readonly ConsulGrayReleaseService? _grayReleaseService;
    /// <summary>当前实例 ID（用于灰度切流判定）。</summary>
    private readonly string _instanceId;
    /// <summary>期望的 Schema 版本（从配置读取，0 表示不校验）。</summary>
    private readonly int _expectedSchemaVersion;
    /// <summary>环境名（如 dev/staging/prod）。</summary>
    private readonly string _env;

    /// <summary>
    /// 主构造函数（DI 生产路径）：注入 <see cref="ConsulReloadableConfigurationProvider"/>，
    /// KV 变更时通过 <see cref="ConsulReloadableConfigurationProvider.SetValue"/> 写入并触发 OnReload。
    /// 阶段四 4.6 步骤6：可选注入 Schema 校验器和灰度发布服务，启用配置版本化与灰度发布。
    /// </summary>
    public ConsulConfigWatcher(
        IConsulClient consul,
        ConsulReloadableConfigurationProvider consulProvider,
        IConfiguration configuration,
        ILogger<ConsulConfigWatcher> logger,
        ConsulConfigSchemaValidator? schemaValidator = null,
        ConsulGrayReleaseService? grayReleaseService = null)
    {
        ArgumentNullException.ThrowIfNull(consul);
        ArgumentNullException.ThrowIfNull(consulProvider);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _consul = consul;
        _consulProvider = consulProvider;
        _configuration = configuration;
        _logger = logger;
        _bcName = configuration["Service:Name"] ?? string.Empty;
        _useGrpcKey = UseGrpcKeyPrefix + _bcName;
        _schemaValidator = schemaValidator;
        _grayReleaseService = grayReleaseService;
        _instanceId = configuration["Service:InstanceId"]
            ?? Environment.MachineName + "-" + Environment.ProcessId;
        _env = configuration["Service:Env"] ?? "dev";
        _expectedSchemaVersion = int.TryParse(configuration["Consul:SchemaVersion"], out var v) ? v : 0;
    }

    /// <summary>
    /// 向后兼容构造函数（测试场景）：不注入 ConsulReloadableConfigurationProvider，
    /// KV 变更时直接写 <see cref="IConfiguration"/> 索引器（依赖 MemoryConfigurationProvider 接受 Set）。
    /// 此路径不触发 IOptionsMonitor 重载，仅用于既有测试兼容。
    /// </summary>
    internal ConsulConfigWatcher(
        IConsulClient consul,
        IConfiguration configuration,
        ILogger<ConsulConfigWatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(consul);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _consul = consul;
        _consulProvider = null;
        _configuration = configuration;
        _logger = logger;
        _bcName = configuration["Service:Name"] ?? string.Empty;
        _useGrpcKey = UseGrpcKeyPrefix + _bcName;
        _schemaValidator = null;
        _grayReleaseService = null;
        _instanceId = configuration["Service:InstanceId"]
            ?? Environment.MachineName + "-" + Environment.ProcessId;
        _env = configuration["Service:Env"] ?? "dev";
        _expectedSchemaVersion = int.TryParse(configuration["Consul:SchemaVersion"], out var v) ? v : 0;
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

                    // 阶段四 4.6 步骤6：灰度发布判定
                    if (_grayReleaseService is not null && !await ShouldApplyByGrayReleaseAsync(ct).ConfigureAwait(false))
                    {
                        _logger.LogInformation("灰度判定：实例 {Instance} 未命中灰度，跳过本次配置应用", _instanceId);
                        continue;
                    }

                    // 阶段四 4.6 步骤6：Schema 版本校验（仅对配置类 KV，UseGrpc 简单布尔值跳过）
                    // UseGrpc KV 值为 "true"/"false"，非 JSON，无需 Schema 校验
                    // Schema 校验适用于 leno/config/{env}/{service} 路径下的 JSON 配置

                    // T19：优先通过 ConsulReloadableConfigurationProvider 写入并触发 OnReload，
                    // 使 IOptionsMonitor<AntiCorruptionOptions> 感知变更并重新绑定 CurrentValue。
                    // 测试路径（_consulProvider 为 null）回退到直接写 IConfiguration 索引器。
                    if (_consulProvider is not null)
                    {
                        _consulProvider.SetValue(UseGrpcConfigKey, newValue);
                    }
                    else
                    {
                        _configuration[UseGrpcConfigKey] = newValue;
                    }
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

    /// <summary>
    /// 阶段四 4.6 步骤6：读取 Consul KV 灰度比例并判定当前实例是否命中灰度。
    /// 灰度比例 key：leno/config/{env}/{service}/gray-percent
    /// </summary>
    private async Task<bool> ShouldApplyByGrayReleaseAsync(CancellationToken ct)
    {
        if (_grayReleaseService is null)
        {
            return true;
        }

        var grayKey = $"{ConfigKeyPrefix}{_env}/{_bcName}{GrayPercentKeySuffix}";
        try
        {
            var grayResult = await _consul.KV.Get(grayKey, ct).ConfigureAwait(false);
            if (grayResult.Response is null)
            {
                // 灰度比例未配置，默认全量应用
                return true;
            }

            var rawPercent = Encoding.UTF8.GetString(grayResult.Response.Value);
            var grayPercent = _grayReleaseService.ParseGrayPercent(rawPercent);
            return _grayReleaseService.ShouldApplyConfig(_instanceId, grayPercent);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "读取灰度比例失败，默认全量应用");
            return true;
        }
    }
}
