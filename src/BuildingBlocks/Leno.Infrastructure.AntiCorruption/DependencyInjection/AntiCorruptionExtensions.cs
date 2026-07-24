using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.AntiCorruption.DependencyInjection;

/// <summary>
/// ACL 防腐层 DI 扩展（阶段四 4.2：可插拔策略链）。
/// <para>
/// 提供扩展方法注册 <see cref="IAclChannel"/> 实现到 DI 容器，
/// 并配置 <see cref="AclChannelRegistry"/> 与 <see cref="AntiCorruptionDispatcher"/>。
/// </para>
/// <para>
/// 典型用法：
/// <code>
/// services.AddAclStrategyChain()
///     .AddGrpcAclChannel("product", productGrpcClient, InvokeProductGrpc)
///     .AddHttpAclChannel("product", httpClient);
/// </code>
/// </para>
/// <para>
/// 与既有 <see cref="AntiCorruptionPollyExtensions.AddLenoAntiCorruptionPolly"/> 共存：
/// - 策略链版本（本扩展）：使用 <see cref="IAclChannel"/> + <see cref="AntiCorruptionDispatcher"/>
/// - 旧版本（既有）：使用 <see cref="AntiCorruptionDispatcher{TService}"/> + <see cref="CircuitBreakerState"/>
/// 双轨期 4 周后下线旧版本。
/// </para>
/// </summary>
public static class AntiCorruptionExtensions
{
    /// <summary>
    /// 注册 ACL 策略链基础设施：AclChannelRegistry + AntiCorruptionDispatcher + AntiCorruptionMetrics.Initialize()。
    /// <para>
    /// 后续通过 <see cref="AddGrpcAclChannel"/> / <see cref="AddHttpAclChannel"/> / <see cref="AddAclChannel"/>
    /// 注册具体通道实现。调用方需在注册完所有通道后才解析 <see cref="AntiCorruptionDispatcher"/>。
    /// </para>
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>构建器对象，用于链式注册通道。</returns>
    public static AclChannelBuilder AddAclStrategyChain(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 确保指标初始化（幂等）
        AntiCorruptionMetrics.Initialize();

        // AclChannelRegistry 在容器首次解析时构造（收集所有 IAclChannel 实现）
        services.TryAddSingleton<AclChannelRegistry>(sp =>
        {
            var channels = sp.GetServices<IAclChannel>();
            var logger = sp.GetService<ILogger<AclChannelRegistry>>();
            var optionsMonitor = sp.GetService<IOptionsMonitor<AntiCorruptionOptions>>();
            return new AclChannelRegistry(channels, logger, optionsMonitor);
        });

        // AntiCorruptionDispatcher 注册为 Singleton（无状态，跨请求复用熔断器）
        services.TryAddSingleton<AntiCorruptionDispatcher>(sp =>
        {
            var registry = sp.GetRequiredService<AclChannelRegistry>();
            var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher>>();
            return new AntiCorruptionDispatcher(registry, logger);
        });

        return new AclChannelBuilder(services);
    }

    /// <summary>
    /// 注册一个自定义 <see cref="IAclChannel"/> 实现到 DI 容器。
    /// </summary>
    /// <typeparam name="TChannel">通道实现类型。</typeparam>
    /// <param name="builder">构建器对象。</param>
    /// <param name="implementationFactory">通道实例工厂。</param>
    /// <returns>构建器对象（链式调用）。</returns>
    public static AclChannelBuilder AddAclChannel<TChannel>(
        this AclChannelBuilder builder,
        Func<IServiceProvider, TChannel> implementationFactory)
        where TChannel : class, IAclChannel
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(implementationFactory);
        builder.Services.AddSingleton<IAclChannel>(implementationFactory);
        return builder;
    }

    /// <summary>
    /// 注册一个 <see cref="GrpcAclChannel"/> 到 DI 容器。
    /// <para>
    /// gRPC 通道优先级默认 0（最高），作为主通道。
    /// </para>
    /// </summary>
    /// <param name="builder">构建器对象。</param>
    /// <param name="serviceName">防腐层服务标识（如 "product"）。</param>
    /// <param name="handler">请求处理委托。</param>
    /// <param name="priority">优先级，默认 0。</param>
    /// <returns>构建器对象（链式调用）。</returns>
    public static AclChannelBuilder AddGrpcAclChannel(
        this AclChannelBuilder builder,
        string serviceName,
        IAclRequestHandler handler,
        int priority = GrpcAclChannel.DefaultPriority)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(handler);
        builder.Services.AddSingleton<IAclChannel>(sp =>
        {
            var logger = sp.GetService<ILogger<GrpcAclChannel>>();
            return new GrpcAclChannel(serviceName, handler, priority, logger);
        });
        return builder;
    }

    /// <summary>
    /// 注册一个绑定到具体 gRPC 客户端的 <see cref="GrpcAclChannel{TClient}"/> 到 DI 容器。
    /// </summary>
    /// <typeparam name="TClient">gRPC 客户端类型。</typeparam>
    /// <param name="builder">构建器对象。</param>
    /// <param name="serviceName">防腐层服务标识。</param>
    /// <param name="client">gRPC 客户端实例。</param>
    /// <param name="invoker">调用委托：将 AclRequest 转换为 gRPC 调用并返回 AclResponse。</param>
    /// <param name="priority">优先级，默认 0。</param>
    /// <returns>构建器对象（链式调用）。</returns>
    public static AclChannelBuilder AddGrpcAclChannel<TClient>(
        this AclChannelBuilder builder,
        string serviceName,
        TClient client,
        Func<TClient, AclRequest, CancellationToken, Task<AclResponse>> invoker,
        int priority = GrpcAclChannel.DefaultPriority)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(invoker);
        builder.Services.AddSingleton<IAclChannel>(sp =>
        {
            var logger = sp.GetService<ILogger<GrpcAclChannel<TClient>>>();
            return new GrpcAclChannel<TClient>(serviceName, client, invoker, priority, logger);
        });
        return builder;
    }

    /// <summary>
    /// 注册一个 <see cref="HttpAclChannel"/> 到 DI 容器，绑定到已配置的 HttpClient。
    /// <para>
    /// HTTP 通道优先级默认 1（次高），作为 gRPC 的降级备份。
    /// 调用方需先通过 <c>AddHttpClient&lt;TClient&gt;(...).AddAntiCorruptionPolicies()</c> 配置 HttpClient。
    /// </para>
    /// </summary>
    /// <param name="builder">构建器对象。</param>
    /// <param name="serviceName">防腐层服务标识。</param>
    /// <param name="httpClient">已配置的 HttpClient 实例。</param>
    /// <param name="priority">优先级，默认 1。</param>
    /// <param name="requestUriBuilder">URI 构造委托；为 null 时使用默认 {BaseAddress}/internal/{OperationName}。</param>
    /// <returns>构建器对象（链式调用）。</returns>
    public static AclChannelBuilder AddHttpAclChannel(
        this AclChannelBuilder builder,
        string serviceName,
        HttpClient httpClient,
        int priority = HttpAclChannel.DefaultPriority,
        Func<AclRequest, Uri>? requestUriBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(httpClient);
        builder.Services.AddSingleton<IAclChannel>(sp =>
        {
            var logger = sp.GetService<ILogger<HttpAclChannel>>();
            return new HttpAclChannel(
                serviceName,
                httpClient,
                requestUriBuilder,
                requestBodyBuilder: null,
                responseParser: null,
                priority,
                logger);
        });
        return builder;
    }

    /// <summary>
    /// 注册一个 <see cref="HttpAclChannel"/> 到 DI 容器，通过 HttpClientFactory 创建 HttpClient。
    /// </summary>
    /// <param name="builder">构建器对象。</param>
    /// <param name="serviceName">防腐层服务标识。</param>
    /// <param name="httpClientName">在 HttpClientFactory 中注册的 HttpClient 名称。</param>
    /// <param name="priority">优先级，默认 1。</param>
    /// <returns>构建器对象（链式调用）。</returns>
    public static AclChannelBuilder AddHttpAclChannel(
        this AclChannelBuilder builder,
        string serviceName,
        string httpClientName,
        int priority = HttpAclChannel.DefaultPriority)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpClientName);
        builder.Services.AddSingleton<IAclChannel>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(httpClientName);
            var logger = sp.GetService<ILogger<HttpAclChannel>>();
            return new HttpAclChannel(
                serviceName,
                httpClient,
                requestUriBuilder: null,
                requestBodyBuilder: null,
                responseParser: null,
                priority,
                logger);
        });
        return builder;
    }

    /// <summary>
    /// 启用 ACL 策略链模式（feature flag），将策略链 dispatcher 注册为默认 dispatcher。
    /// <para>
    /// 通过 <c>AntiCorruption:UseStrategyChain</c> 配置项控制（默认 false）。
    /// 启用后旧 <see cref="AntiCorruptionDispatcher{TService}"/> 仍可用，新代码应使用非泛型 <see cref="AntiCorruptionDispatcher"/>。
    /// </para>
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">应用配置。</param>
    /// <returns>服务集合（链式调用）。</returns>
    public static IServiceCollection ConfigureAclStrategyChain(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 绑定 AntiCorruptionOptions.UseStrategyChain（向后兼容：默认 false）
        services.Configure<AntiCorruptionOptions>(configuration.GetSection("AntiCorruption"));

        return services;
    }
}

/// <summary>
/// ACL 通道构建器：用于链式注册 IAclChannel 实现。
/// 由 <see cref="AntiCorruptionExtensions.AddAclStrategyChain"/> 返回。
/// </summary>
public sealed class AclChannelBuilder
{
    internal AclChannelBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    internal IServiceCollection Services { get; }
}
