using Leno.Infrastructure.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Infrastructure.Caching;

/// <summary>
/// 缓存服务 DI 注册扩展方法。
/// <para>
/// 提供 <see cref="AddRedisCache"/> 注册 L2 Redis 缓存（<see cref="ICacheService"/> + <see cref="IBloomFilter"/>），
/// 以及 <see cref="AddMultiLevelCache"/> 在 L2 基础上叠加 L1 本地缓存 + Pub/Sub 跨实例失效。
/// </para>
/// </summary>
public static class CacheServiceCollectionExtensions
{
    /// <summary>
    /// 注册 L2 Redis 缓存基础设施：Redis 连接、布隆过滤器、<see cref="ICacheService"/>。
    /// <para>
    /// 各 BC 在自己的 <c>AddXxxInfrastructure</c> 中调用此方法以启用 Redis 缓存能力。
    /// </para>
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">应用配置，用于读取 <c>Redis:Configuration</c> 连接串。</param>
    /// <returns>服务集合（链式调用）。</returns>
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var redisConfig = configuration["Redis:Configuration"] ?? "localhost:6379";

        // T21：使用 Lazy<IConnectionMultiplexer> 延迟连接，避免应用启动时同步阻塞主线程。
        // LazyThreadSafetyMode.ExecutionAndPublication 保证多线程下仅初始化一次。
        var lazyMultiplexer = new Lazy<IConnectionMultiplexer>(
            () => ConnectionMultiplexer.Connect(redisConfig),
            LazyThreadSafetyMode.ExecutionAndPublication);

        services.TryAddSingleton<IConnectionMultiplexer>(_ => lazyMultiplexer.Value);
        services.TryAddSingleton<IBloomFilter>(sp =>
        {
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<RedisBloomFilter>>();
            return new RedisBloomFilter(redis, logger);
        });
        // CacheService 标记为 [Obsolete]（阶段四步骤 4.5 双轨期 4 周），
        // 但作为 ICacheService 的默认实现仍需注册到 DI。新代码应使用 IMultiLevelCache。
#pragma warning disable CS0618
        services.TryAddSingleton<ICacheService>(sp =>
        {
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            var bloomFilter = sp.GetRequiredService<IBloomFilter>();
            var logger = sp.GetRequiredService<ILogger<CacheService>>();
            return new CacheService(redis, bloomFilter, logger);
        });
#pragma warning restore CS0618

        return services;
    }

    /// <summary>
    /// 注册多级缓存（L1 IMemoryCache + L2 Redis + Pub/Sub 跨实例失效）。
    /// <para>
    /// 在 <see cref="AddRedisCache"/> 基础上叠加：
    /// <list type="bullet">
    /// <item><see cref="IMemoryCache"/>：L1 进程内本地缓存（短 TTL，默认 5s）。</item>
    /// <item><see cref="ICacheInvalidationPublisher"/> / <see cref="CacheInvalidationPublisher"/>：Pub/Sub 失效通知发布者。</item>
    /// <item><see cref="CacheInvalidationSubscriber"/>：作为 <c>IHostedService</c> 后台运行，订阅 Pub/Sub 通道清本地 L1。</item>
    /// <item><see cref="IMultiLevelCache"/> / <see cref="MultiLevelCache"/>：多级缓存门面，业务代码通过此接口读写缓存。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 配置节：<c>Cache:MultiLevel</c>（见 <see cref="MultiLevelCacheOptions.SectionName"/>）。
    /// </para>
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">应用配置，用于读取 <c>Cache:MultiLevel</c> 节与 <c>Redis:Configuration</c> 连接串。</param>
    /// <returns>服务集合（链式调用）。</returns>
    public static IServiceCollection AddMultiLevelCache(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 确保 L2 Redis 基础设施已注册（幂等）
        services.AddRedisCache(configuration);

        // L1 本地缓存（IMemoryCache 由 Microsoft.Extensions.Caching.Memory 提供，
        // FrameworkReference Microsoft.AspNetCore.App 已包含）
        services.TryAddSingleton<IMemoryCache, MemoryCache>();

        // 多级缓存配置
        services.Configure<MultiLevelCacheOptions>(configuration.GetSection(MultiLevelCacheOptions.SectionName));

        // Pub/Sub 失效通知发布者
        services.TryAddSingleton<ICacheInvalidationPublisher, CacheInvalidationPublisher>();

        // Pub/Sub 失效通知订阅者（IHostedService 后台运行）
        services.AddHostedService<CacheInvalidationSubscriber>();
        services.TryAddSingleton<CacheInvalidationSubscriber>();

        // 多级缓存门面
        services.TryAddSingleton<IMultiLevelCache, MultiLevelCache>();

        return services;
    }
}
