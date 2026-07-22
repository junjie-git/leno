using Leno.Infrastructure.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Infrastructure.RateLimiting;

/// <summary>
/// 限流器 DI 注册扩展方法。
/// </summary>
public static class RateLimiterExtensions
{
    /// <summary>
    /// 注册共享 Redis 滑动窗口限流器到 DI 容器。
    /// 各 BC 注入 <see cref="IRateLimiter"/> 即可使用。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="redis">Redis 连接复用器（通常由 <c>AddRedis</c> 注册）。</param>
    /// <returns>服务集合（链式调用）。</returns>
    public static IServiceCollection AddRedisRateLimiter(
        this IServiceCollection services,
        IConnectionMultiplexer redis)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(redis);

        services.AddSingleton<IRateLimiter>(provider =>
        {
            var logger = provider.GetService<ILogger<RedisSlidingWindowRateLimiter>>();
            return new RedisSlidingWindowRateLimiter(redis, logger);
        });

        return services;
    }
}
