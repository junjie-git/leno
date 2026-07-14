namespace Leno.ApiGateway.Options;

/// <summary>
/// 限流配置根节，对应 appsettings.json 中 <c>RateLimit</c> 节。
/// 三层策略：全局令牌桶 → 路由滑动窗口 → 用户滑动窗口。
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>全局令牌桶策略，保护网关整体容量。</summary>
    public GlobalRateLimitOptions Global { get; set; } = new();

    /// <summary>按路由的滑动窗口策略映射，Key 为策略名（与路由 RateLimiterPolicy 字段对应）。</summary>
    public Dictionary<string, RouteRateLimitOptions> Routes { get; set; } = new();

    /// <summary>按用户的滑动窗口策略。</summary>
    public UserRateLimitOptions User { get; set; } = new();

    /// <summary>Redis 分布式限流是否启用（多实例部署时为 true）。</summary>
    public bool UseRedisDistributed { get; set; } = true;

    /// <summary>Redis 限流计数器 Key 前缀。</summary>
    public string RedisKeyPrefix { get; set; } = "leno:ratelimit:";
}

/// <summary>全局令牌桶配置。</summary>
public sealed class GlobalRateLimitOptions
{
    /// <summary>令牌桶容量（最大瞬时请求数）。</summary>
    public int TokenLimit { get; set; } = 5000;

    /// <summary>每周期补充令牌数。</summary>
    public int TokensPerPeriod { get; set; } = 5000;

    /// <summary>补充周期（默认 1 秒，即 5000 req/s）。</summary>
    public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>是否自动排队（false 表示超出立即拒绝）。</summary>
    public bool AutoReplenishment { get; set; } = true;

    /// <summary>队列长度（AutoReplenishment=true 时生效，0 表示不排队）。</summary>
    public int QueueLimit { get; set; }
}

/// <summary>按路由滑动窗口配置。</summary>
public sealed class RouteRateLimitOptions
{
    /// <summary>窗口内最大请求数。</summary>
    public int PermitLimit { get; set; }

    /// <summary>滑动窗口时长。</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>窗口分段数（影响精度，越大越精确但内存占用越高）。</summary>
    public int SegmentsPerWindow { get; set; } = 4;

    /// <summary>队列长度（0 表示超出立即拒绝）。</summary>
    public int QueueLimit { get; set; }
}

/// <summary>按用户滑动窗口配置（基于 JWT 中的 Sub claim 或 X-User-Id 头）。</summary>
public sealed class UserRateLimitOptions
{
    /// <summary>每用户每窗口最大请求数（默认 100 req/min）。</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>窗口时长（默认 1 分钟）。</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>窗口分段数。</summary>
    public int SegmentsPerWindow { get; set; } = 6;

    /// <summary>未认证请求的分区 Key（用客户端 IP 兜底）。</summary>
    public string AnonymousPartitionClaim { get; set; } = "client-ip";

    /// <summary>队列长度。</summary>
    public int QueueLimit { get; set; }
}
