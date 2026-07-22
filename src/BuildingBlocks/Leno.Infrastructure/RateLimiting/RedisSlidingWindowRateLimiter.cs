using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Infrastructure.RateLimiting;

/// <summary>
/// 基于 Redis SortedSet + Lua 脚本的分布式滑动窗口限流器。
/// <para>
/// 算法：
/// 1. 以 SortedSet 存储请求时间戳作为 member，时间戳（毫秒）作为 score。
/// 2. Lua 脚本原子执行：ZREMRANGEBYSCORE 清除窗口外旧记录 → ZCARD 计数 → 判断是否超过阈值 → ZADD 当前时间戳。
/// 3. 超过阈值时拒绝且不写入；首次访问时设置 TTL 避免键永久驻留。
/// </para>
/// Redis 不可用时 fail-open 放行并记录 warning，避免限流器故障阻断全部流量。
/// </summary>
public sealed class RedisSlidingWindowRateLimiter : IRateLimiter
{
    // KEYS[1] = Redis key
    // ARGV[1] = current timestamp (ms, score)
    // ARGV[2] = window start timestamp (ms, ZREMRANGEBYSCORE lower bound)
    // ARGV[3] = current timestamp string (member, must be unique → use counter)
    // ARGV[4] = permit limit
    // ARGV[5] = key TTL in seconds
    // 修复（fix-12 P0-T8）：必须先 ZREMRANGEBYSCORE 清除窗口外过期记录，再 ZCARD 计数。
    // 原实现先 ZCARD 后 ZREMRANGEBYSCORE，第一次计数包含已过期但未清理的旧记录，
    // 导致窗口边界附近误拒合法请求。
    private const string Script = @"
redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, ARGV[2])
local count = redis.call('ZCARD', KEYS[1])
if count >= tonumber(ARGV[4]) then
    return 0
end
redis.call('ZADD', KEYS[1], ARGV[1], ARGV[3])
if count == 0 then
    redis.call('EXPIRE', KEYS[1], ARGV[5])
end
local newCount = redis.call('ZCARD', KEYS[1])
if newCount > tonumber(ARGV[4]) then
    redis.call('ZREM', KEYS[1], ARGV[3])
    return 0
end
return newCount
";

    /// <summary>
    /// 暴露 Lua 脚本用于测试验证脚本顺序（internal 可见性，仅测试使用）。
    /// </summary>
    internal static string GetScriptForTesting() => Script;

    private const string KeyPrefix = "leno:ratelimit:";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisSlidingWindowRateLimiter> _logger;
    private long _counter;

    /// <summary>
    /// 创建限流器实例。
    /// </summary>
    /// <param name="redis">Redis 连接复用器。</param>
    /// <param name="logger">日志记录器。null 时使用 NullLogger。</param>
    public RedisSlidingWindowRateLimiter(
        IConnectionMultiplexer redis,
        ILogger<RedisSlidingWindowRateLimiter>? logger = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? NullLogger<RedisSlidingWindowRateLimiter>.Instance;
        _counter = 0;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> TryAcquireAsync(
        string key,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("限流键不能为空", nameof(key));
        }

        if (permitLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(permitLimit), "许可数必须大于 0");
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "窗口时长必须大于 0");
        }

        var redisKey = KeyPrefix + key;

        try
        {
            var db = _redis.GetDatabase();
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var windowStartMs = nowMs - (long)window.TotalMilliseconds;
            var member = $"{nowMs}:{Interlocked.Increment(ref _counter)}";
            var ttlSeconds = (int)Math.Ceiling(window.TotalSeconds * 1.1); // 留 10% 余量避免提前过期

            var result = (long)await db.ScriptEvaluateAsync(
                Script,
                new RedisKey[] { redisKey },
                new RedisValue[]
                {
                    nowMs,
                    windowStartMs,
                    member,
                    permitLimit,
                    ttlSeconds
                }).ConfigureAwait(false);

            var resetAt = DateTime.UtcNow.Add(window);

            if (result == 0L)
            {
                _logger.LogWarning(
                    "限流拒绝 Key={Key} Limit={Limit} Window={Window}",
                    key, permitLimit, window);
                return RateLimitResult.Denied(permitLimit, permitLimit, resetAt);
            }

            var currentCount = (int)result;
            return RateLimitResult.Acquired(currentCount, permitLimit, resetAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Redis 不可用时 fail-open 放行，避免限流器故障阻断全部流量。
            _logger.LogWarning(ex,
                "Redis 滑动窗口限流器异常，fail-open 放行。Key={Key} PermitLimit={Limit} Window={Window}",
                key, permitLimit, window);
            return RateLimitResult.Acquired(0, permitLimit, DateTime.UtcNow.Add(window));
        }
    }
}
