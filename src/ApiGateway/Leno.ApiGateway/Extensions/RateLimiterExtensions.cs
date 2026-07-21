using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace Leno.ApiGateway.Extensions;

/// <summary>
/// 网关限流服务注册扩展。
/// 注册 ASP.NET Core <c>AddRateLimiter</c> 中间件，包含三层策略：
/// "global"（全局令牌桶）、"default"（普通路由滑动窗口）、"seckill"（秒杀滑动窗口）、"per-user"（按用户滑动窗口）。
/// </summary>
public static class RateLimiterExtensions
{
    /// <summary>限流策略名常量，与 appsettings.json 中 ReverseProxy:Routes[*].RateLimiterPolicy 字段对应。</summary>
    public static class Policies
    {
        public const string Global = "global";
        public const string Default = "leno-default";
        public const string Seckill = "seckill";
        public const string PerUser = "per-user";
    }

    /// <summary>
    /// 注册网关三层限流策略：
    /// <list type="bullet">
    /// <item>GlobalLimiter：令牌桶 5000 req/s 保护整体容量</item>
    /// <item>路由级策略 "default"/"seckill"：滑动窗口 200/50 req/s</item>
    /// <item>用户级策略 "per-user"：滑动窗口 100 req/min，按 UserId 分区</item>
    /// </list>
    /// Redis 启用时（UseRedisDistributed=true），路由级和用户级策略使用 <see cref="RedisSlidingWindowRateLimiter"/>。
    /// </summary>
    public static IServiceCollection AddGatewayRateLimiter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<RateLimitOptions>(configuration.GetSection("RateLimit"));

        var rateLimitOptions = configuration.GetSection("RateLimit").Get<RateLimitOptions>()
            ?? new RateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // 全局令牌桶：保护网关整体容量
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext =>
                {
                    var partitionKey = "global";
                    return RateLimitPartition.GetTokenBucketLimiter(
                        partitionKey,
                        _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = rateLimitOptions.Global.TokenLimit,
                            TokensPerPeriod = rateLimitOptions.Global.TokensPerPeriod,
                            ReplenishmentPeriod = rateLimitOptions.Global.ReplenishmentPeriod,
                            AutoReplenishment = rateLimitOptions.Global.AutoReplenishment,
                            QueueLimit = rateLimitOptions.Global.QueueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                        });
                });

            // 路由级滑动窗口：普通接口
            options.AddPolicy(Policies.Default, httpContext =>
                CreateSlidingWindowPartition(
                    httpContext,
                    rateLimitOptions,
                    policyName: Policies.Default,
                    partitionKeyFactory: ctx => GetClientIdentifier(ctx, rateLimitOptions.User.AnonymousPartitionClaim)));

            // 路由级滑动窗口：秒杀接口
            options.AddPolicy(Policies.Seckill, httpContext =>
                CreateSlidingWindowPartition(
                    httpContext,
                    rateLimitOptions,
                    policyName: Policies.Seckill,
                    partitionKeyFactory: ctx => GetClientIdentifier(ctx, rateLimitOptions.User.AnonymousPartitionClaim)));

            // 用户级滑动窗口：100 req/min per UserId
            options.AddPolicy(Policies.PerUser, httpContext =>
                CreateSlidingWindowPartition(
                    httpContext,
                    rateLimitOptions,
                    policyName: Policies.PerUser,
                    partitionKeyFactory: ctx => GetClientIdentifier(ctx, rateLimitOptions.User.AnonymousPartitionClaim)));
        });

        return services;
    }

    /// <summary>
    /// 根据配置创建滑动窗口分区：Redis 启用时返回 <see cref="RedisSlidingWindowRateLimiter"/>，否则回退到内置 <see cref="SlidingWindowRateLimiter"/>。
    /// </summary>
    private static RateLimitPartition<string> CreateSlidingWindowPartition(
        HttpContext httpContext,
        RateLimitOptions options,
        string policyName,
        Func<HttpContext, string> partitionKeyFactory)
    {
        var partitionKey = partitionKeyFactory(httpContext);
        var compositeKey = $"{options.RedisKeyPrefix}{policyName}:{partitionKey}";

        // 路由级策略从 Routes 字典查找，用户级策略从 User 字段查找
        var (permitLimit, window, segmentsPerWindow) = policyName switch
        {
            Policies.PerUser => (options.User.PermitLimit, options.User.Window, options.User.SegmentsPerWindow),
            _ when options.Routes.TryGetValue(policyName, out var routeOpts)
                => (routeOpts.PermitLimit, routeOpts.Window, routeOpts.SegmentsPerWindow),
            _ => (200, TimeSpan.FromSeconds(1), 4)
        };

        if (options.UseRedisDistributed)
        {
            var database = httpContext.RequestServices.GetRequiredService<IDatabase>();
            // T28：从 DI 解析 logger 传入限流器，使 Redis 异常时能记录 warning 日志
            var logger = httpContext.RequestServices.GetService<ILogger<RedisSlidingWindowRateLimiter>>();
            return RateLimitPartition.Get(
                compositeKey,
                _ => new RedisSlidingWindowRateLimiter(
                    database,
                    compositeKey,
                    permitLimit,
                    window,
                    segmentsPerWindow,
                    logger));
        }

        return RateLimitPartition.GetSlidingWindowLimiter(
            compositeKey,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                SegmentsPerWindow = segmentsPerWindow,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    }

    /// <summary>
    /// 提取客户端标识：优先 JWT Sub claim，其次 X-User-Id 头，最后回退到客户端 IP（用于匿名用户）。
    /// </summary>
    private static string GetClientIdentifier(HttpContext context, string anonymousClaim)
    {
        // JWT Sub claim（阶段二 JwtAuthMiddleware 注入）
        var subClaim = context.User.FindFirst("Sub")?.Value;
        if (!string.IsNullOrEmpty(subClaim))
        {
            return subClaim;
        }

        // X-User-Id 头（阶段二 UserContextTransform 注入）
        if (context.Request.Headers.TryGetValue("X-User-Id", out var userIdHeader)
            && !string.IsNullOrEmpty(userIdHeader))
        {
            return userIdHeader.ToString();
        }

        // 回退到客户端 IP
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"{anonymousClaim}:{clientIp}";
    }
}
