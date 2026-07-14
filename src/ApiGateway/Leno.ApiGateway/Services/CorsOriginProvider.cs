using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Consul;
using Leno.ApiGateway.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Services;

/// <summary>
/// CORS Origin 列表提供者。从 Consul KV 读取允许的 Origin，支持热更新。
/// </summary>
public interface ICorsOriginProvider
{
    /// <summary>当前允许的 Origin 列表（只读快照）。</summary>
    IReadOnlyList<string> AllowedOrigins { get; }

    /// <summary>判断指定 Origin 是否被允许。</summary>
    bool IsOriginAllowed(string origin);

    /// <summary>从 Consul KV 重新加载 Origin 列表。</summary>
    Task RefreshAsync(CancellationToken ct);
}

/// <summary>
/// 基于 Consul KV 的 CORS Origin 提供者。
/// <para>
/// 构造时使用配置文件中的 <see cref="CorsOptions.AllowedOrigins"/> 初始化，
/// 随后由 <see cref="CorsOriginRefreshService"/> 定时从 Consul KV 刷新。
/// Origin 列表存储于线程安全的 <see cref="ConcurrentDictionary{TKey, TValue}"/> 中。
/// </para>
/// </summary>
public sealed class ConsulCorsOriginProvider : ICorsOriginProvider
{
    private readonly IConsulClient _consul;
    private readonly CorsOptions _options;
    private readonly ILogger<ConsulCorsOriginProvider> _logger;
    private readonly ConcurrentDictionary<string, byte> _origins =
        new(StringComparer.OrdinalIgnoreCase);

    public ConsulCorsOriginProvider(
        IConsulClient consul,
        IOptions<CorsOptions> options,
        ILogger<ConsulCorsOriginProvider> logger)
    {
        _consul = consul ?? throw new ArgumentNullException(nameof(consul));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 初始化使用配置文件中的默认 Origins
        foreach (var origin in _options.AllowedOrigins)
        {
            if (!string.IsNullOrWhiteSpace(origin))
            {
                _origins.TryAdd(origin, 0);
            }
        }
    }

    public IReadOnlyList<string> AllowedOrigins => _origins.Keys.ToList();

    public bool IsOriginAllowed(string origin)
    {
        if (string.IsNullOrEmpty(origin))
        {
            return false;
        }
        return _origins.ContainsKey(origin);
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var result = await _consul.KV.Get(_options.ConsulKvKey, ct);

            if (result.Response is null)
            {
                _logger.LogWarning(
                    "Consul KV key {Key} not found, keeping existing origins", _options.ConsulKvKey);
                return;
            }

            var json = Encoding.UTF8.GetString(result.Response.Value);
            var origins = JsonSerializer.Deserialize<string[]>(json);

            if (origins is null || origins.Length == 0)
            {
                _logger.LogWarning(
                    "Consul KV key {Key} returned empty origin list", _options.ConsulKvKey);
                return;
            }

            // 原子替换：清空后重新填充
            _origins.Clear();
            foreach (var origin in origins)
            {
                if (!string.IsNullOrWhiteSpace(origin))
                {
                    _origins.TryAdd(origin, 0);
                }
            }

            _logger.LogInformation(
                "Refreshed {Count} CORS origins from Consul KV {Key}",
                origins.Length, _options.ConsulKvKey);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 刷新失败不覆盖现有 Origins，保留上次成功状态
            _logger.LogError(ex,
                "Failed to refresh CORS origins from Consul KV {Key}", _options.ConsulKvKey);
        }
    }
}

/// <summary>
/// 定时从 Consul KV 刷新 CORS Origin 列表的托管服务。
/// </summary>
public sealed class CorsOriginRefreshService : BackgroundService
{
    private readonly ICorsOriginProvider _provider;
    private readonly CorsOptions _options;
    private readonly ILogger<CorsOriginRefreshService> _logger;

    public CorsOriginRefreshService(
        ICorsOriginProvider provider,
        IOptions<CorsOptions> options,
        ILogger<CorsOriginRefreshService> logger)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动时立即刷新一次
        await _provider.RefreshAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.RefreshInterval, stoppingToken);
                await _provider.RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

/// <summary>
/// 通过 <see cref="IConfigureOptions{TOptions}"/> 在运行时动态配置 CORS 策略。
/// <para>
/// 配置目标为 ASP.NET Core 框架的
/// <see cref="Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions"/>（用于注册默认 CORS 策略），
/// 而 <see cref="AllowCredentials"/>/<see cref="PreflightMaxAge"/> 等自定义值来自
/// <see cref="Leno.ApiGateway.Options.CorsOptions"/>（通过 DI 注入）。
/// </para>
/// <para>
/// 使用 <see cref="ICorsOriginProvider.IsOriginAllowed"/> 作为 <c>SetIsOriginAllowed</c> 回调，
/// 实现 Origin 列表从 Consul KV 热更新而无需重启网关。
/// </para>
/// </summary>
public sealed class ConfigureGatewayCors
    : IConfigureOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>
{
    private readonly IServiceProvider _serviceProvider;

    public ConfigureGatewayCors(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public void Configure(Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions options)
    {
        // 从 DI 解析自定义 CorsOptions（Leno.ApiGateway.Options.CorsOptions），
        // 框架的 CorsOptions 仅作为 AddDefaultPolicy 的载体。
        using var scope = _serviceProvider.CreateScope();
        var custom = scope.ServiceProvider
            .GetRequiredService<IOptions<CorsOptions>>()
            .Value;

        options.AddDefaultPolicy(policy =>
        {
            policy.SetIsOriginAllowed(origin =>
            {
                // 每次 CORS 请求时通过新 scope 解析单例 ICorsOriginProvider，
                // ConsulCorsOriginProvider 内部使用 ConcurrentDictionary，热更新立即生效。
                using var originScope = _serviceProvider.CreateScope();
                var provider = originScope.ServiceProvider.GetRequiredService<ICorsOriginProvider>();
                return provider.IsOriginAllowed(origin);
            });

            if (custom.AllowAnyMethod)
            {
                policy.AllowAnyMethod();
            }

            if (custom.AllowAnyHeader)
            {
                policy.AllowAnyHeader();
            }

            if (custom.AllowCredentials)
            {
                policy.AllowCredentials();
            }

            policy.SetPreflightMaxAge(custom.PreflightMaxAge);
        });
    }
}
