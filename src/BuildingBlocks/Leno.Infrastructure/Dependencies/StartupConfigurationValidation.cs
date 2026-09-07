using Leno.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.Dependencies;

/// <summary>
/// BC 服务名 → 连接串键 的解析结果与启动配置校验（P0-C fail-fast）。
/// </summary>
/// <remarks>
/// <para>
/// 背景（架构师评审 P0）：各 BC Infrastructure 层读取 <c>ConnectionStrings:{Bc}Db</c>、
/// <c>Redis:Configuration</c>、<c>RabbitMQ:Host</c>，且缺失时静默回退 localhost 默认值
/// （Redis → <c>localhost:6379</c>，RabbitMQ → <c>localhost/guest</c>）。生产环境若 Helm 注入
/// 键名错误或 Consul KV 未种子化，服务会"正常启动"却连不上依赖。本校验在宿主启动阶段
/// （非 Development 环境）对关键配置做 fail-fast，缺失或仍为本地默认值时抛出明确异常拒绝启动。
/// </para>
/// <para>
/// Development 环境完全跳过校验，不破坏本地开发体验（本地 appsettings.json 默认 localhost）。
/// </para>
/// </summary>
public static class LenoStartupConfigurationValidator
{
    /// <summary>
    /// 规范化服务名（去 "leno-" 前缀 / "-api" 后缀 / 去连字符，小写）→ 连接串键。
    /// 键名逐一对照各 BC Infrastructure 层 <c>GetConnectionString("{Bc}Db")</c> 的实际读取键，
    /// 详见 deploy/docs/consul-kv-coverage-audit.md §3.1。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ServiceNameToConnectionStringKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["accesscontrol"] = "AccessControlDb",
            ["aftersales"] = "AfterSalesDb",
            ["cart"] = "CartDb",
            ["identity"] = "IdentityDb",
            ["inventory"] = "InventoryDb",
            ["membership"] = "MembershipDb",
            ["notification"] = "NotificationDb",
            ["order"] = "OrderDb",
            ["payment"] = "PaymentDb",
            ["points"] = "PointsDb",
            ["pointsmembership"] = "PointsMembershipDb",
            ["product"] = "ProductDb",
            ["promotion"] = "PromotionDb",
            ["review"] = "ReviewDb",
            // 注意：reviewaftersales 的连接串键是 ReviewAfterSalesDb（非 ReviewaftersalesDb）
            ["reviewaftersales"] = "ReviewAfterSalesDb",
            ["sellershop"] = "SellerShopDb",
            ["systemadmin"] = "SystemAdminDb",
            ["userauth"] = "UserAuthDb",
            ["usercenter"] = "UserCenterDb"
        };

    /// <summary>
    /// 由服务名（如 <c>leno-order-api</c>）解析该 BC 的连接串键（如 <c>OrderDb</c>）。
    /// 无法识别的服务名返回 null（调用方跳过连接串校验，不影响其他键校验）。
    /// </summary>
    public static string? ResolveConnectionStringKey(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return null;
        }

        var normalized = serviceName.Trim();
        if (normalized.StartsWith("leno-", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["leno-".Length..];
        }

        if (normalized.EndsWith("-api", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^"-api".Length];
        }

        normalized = normalized.Replace("-", string.Empty);
        return ServiceNameToConnectionStringKey.TryGetValue(normalized, out var key) ? key : null;
    }

    /// <summary>
    /// 收集缺失或仍为本地默认值的关键配置键。
    /// </summary>
    /// <param name="configuration">应用配置（含 Consul KV 源，启动时已可用）。</param>
    /// <param name="serviceName">BC 服务名（如 <c>leno-order-api</c>），用于解析各自连接串键。</param>
    /// <returns>问题清单；空列表表示校验通过。</returns>
    public static IReadOnlyList<string> GetInvalidConfigurationKeys(IConfiguration configuration, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var problems = new List<string>();

        // 1. 各 BC 自己的数据库连接串：缺失/为空，或仍指向 localhost（KV/env 未注入）
        var csKey = ResolveConnectionStringKey(serviceName);
        if (csKey is not null)
        {
            var configKey = $"ConnectionStrings:{csKey}";
            var cs = configuration.GetConnectionString(csKey);
            if (string.IsNullOrWhiteSpace(cs))
            {
                problems.Add($"{configKey}（缺失，env 键 ConnectionStrings__{csKey}）");
            }
            else if (ContainsLocalHost(cs))
            {
                problems.Add($"{configKey}（仍为本地默认值，生产应指向集群内 SQL Server 或由 Consul KV 提供）");
            }
        }

        // 2. Redis：缺失时代码静默回退 localhost:6379（ServiceCollectionExtensions.AddRedis）
        var redis = configuration["Redis:Configuration"];
        if (string.IsNullOrWhiteSpace(redis))
        {
            problems.Add("Redis:Configuration（缺失，env 键 Redis__Configuration）");
        }
        else if (redis.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
                 || redis.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("Redis:Configuration（仍为本地默认值）");
        }

        // 3. RabbitMQ：缺失时代码静默回退 localhost/guest（ServiceCollectionExtensions.AddEventBus）
        var rabbitHost = configuration["RabbitMQ:Host"];
        if (string.IsNullOrWhiteSpace(rabbitHost))
        {
            problems.Add("RabbitMQ:Host（缺失，env 键 RabbitMQ__Host）");
        }
        else if (rabbitHost.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                 || rabbitHost.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("RabbitMQ:Host（仍为本地默认值）");
        }

        return problems;
    }

    private static bool ContainsLocalHost(string value) =>
        value.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        || value.Contains("127.0.0.1", StringComparison.Ordinal);
}

/// <summary>
/// 启动配置校验宿主服务（P0-C fail-fast）。
/// 在 <see cref="WebApplicationExtensions.AddLenoApi{TDbContext}"/> 中<strong>最先</strong>注册，
/// 宿主 StartAsync 按注册顺序启动托管服务 → 本服务先于 MassTransit/Consul 监听等启动；
/// 校验失败抛出 <see cref="InvalidOperationException"/>，宿主启动中止（拒绝服务）。
/// </summary>
public sealed class StartupConfigurationValidationService : IHostedService
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly string _serviceName;
    private readonly ILogger<StartupConfigurationValidationService> _logger;

    /// <summary>
    /// 构造函数。DI 容器按注册顺序实例化 <see cref="IHostedService"/>。
    /// </summary>
    /// <param name="environment">宿主环境（Development 跳过校验）。</param>
    /// <param name="configuration">应用配置（ConfigurationManager 引用，启动时已含 KV 源）。</param>
    /// <param name="serviceName">BC 服务名（来自 <see cref="WebApplicationExtensions.AddLenoApi{TDbContext}"/> 参数）。</param>
    /// <param name="logger">日志。</param>
    public StartupConfigurationValidationService(
        IHostEnvironment environment,
        IConfiguration configuration,
        string serviceName,
        ILogger<StartupConfigurationValidationService> logger)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Development 保持现状：本地默认 localhost 配置合法，不破坏开发体验
        if (_environment.IsDevelopment())
        {
            _logger.LogDebug(
                "Development 环境跳过启动配置校验 ServiceName={ServiceName}", _serviceName);
            return Task.CompletedTask;
        }

        var problems = LenoStartupConfigurationValidator.GetInvalidConfigurationKeys(_configuration, _serviceName);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"[Leno 启动校验失败] {_serviceName}（{_environment.EnvironmentName}）关键配置缺失或仍为本地默认值，拒绝启动。" +
                $"请通过 Consul KV（leno/config/**，生产主通道）或环境变量兜底注入：{string.Join("；", problems)}");
        }

        _logger.LogInformation(
            "启动配置校验通过 ServiceName={ServiceName} Environment={Environment}", _serviceName, _environment.EnvironmentName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// <see cref="IServiceCollection"/> 扩展：注册启动配置校验托管服务（P0-C）。
/// 必须在 <see cref="WebApplicationExtensions.AddLenoApi{TDbContext}"/> 的<strong>第一步</strong>调用，
/// 以保证本服务先于 AddLenoInfrastructure（MassTransit）注册的托管服务启动。
/// </summary>
public static class StartupConfigurationValidationExtensions
{
    /// <summary>
    /// 注册启动配置校验宿主服务（非 Development 环境关键配置缺失时 fail-fast）。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">应用配置。</param>
    /// <param name="serviceName">BC 服务名（如 <c>leno-order-api</c>）。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    public static IServiceCollection AddLenoStartupConfigurationValidation(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddSingleton<IHostedService>(
            sp => new StartupConfigurationValidationService(
                sp.GetRequiredService<IHostEnvironment>(),
                configuration,
                serviceName,
                sp.GetRequiredService<ILogger<StartupConfigurationValidationService>>()));

        return services;
    }
}
