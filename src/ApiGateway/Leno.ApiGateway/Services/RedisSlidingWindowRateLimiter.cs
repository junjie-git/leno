using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 基于 Redis SortedSet + Lua 脚本的分布式滑动窗口限流器。
/// <para>
/// 算法：
/// 1. 以 SortedSet 存储请求时间戳作为 member，时间戳（毫秒）作为 score。
/// 2. Lua 脚本原子执行：ZREMRANGEBYSCORE 清除窗口外旧记录 → ZADD 当前时间戳 → ZCARD 计数 → 判断是否超过阈值。
/// 3. 超过阈值时 TTL 仅在 ZCARD=0 时设置（首次访问），避免重复设置。
/// </para>
/// 与 ASP.NET Core <see cref="RateLimiter"/> 抽象兼容，
/// 通过 <see cref="RateLimitPartition.Create{TKey}"/> 注册到 <c>AddRateLimiter</c> 中间件。
/// </summary>
public sealed class RedisSlidingWindowRateLimiter : RateLimiter
{
    // KEYS[1] = Redis key
    // ARGV[1] = current timestamp (ms, score)
    // ARGV[2] = window start timestamp (ms, ZREMRANGEBYSCORE lower bound)
    // ARGV[3] = current timestamp string (member, must be unique → use counter)
    // ARGV[4] = permit limit
    // ARGV[5] = key TTL in seconds
    // 修复：必须先 ZREMRANGEBYSCORE 清除窗口外过期记录，再 ZCARD 计数。
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
return 1
";

    /// <summary>
    /// 暴露 Lua 脚本用于测试验证脚本顺序（internal 可见性，仅测试使用）。
    /// </summary>
    internal static string GetScriptForTesting() => Script;

    private readonly IDatabase _database;
    private readonly string _key;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly int _segmentsPerWindow;
    private readonly ILogger<RedisSlidingWindowRateLimiter> _logger;
    private long _counter;

    /// <summary>
    /// 创建限流器实例。
    /// </summary>
    /// <param name="database">Redis 数据库连接。</param>
    /// <param name="key">Redis Key，应包含策略名 + 分区 Key（如 <c>leno:ratelimit:seckill:user-123</c>）。</param>
    /// <param name="permitLimit">窗口内最大请求数。</param>
    /// <param name="window">滑动窗口时长。</param>
    /// <param name="segmentsPerWindow">窗口分段数（仅用于 TTL 计算，Redis SortedSet 滑动窗口本身无分段概念）。</param>
    /// <param name="logger">日志记录器。T28：Redis 异常时记录 warning 便于运维感知限流器故障。null 时使用 NullLogger。</param>
    public RedisSlidingWindowRateLimiter(
        IDatabase database,
        string key,
        int permitLimit,
        TimeSpan window,
        int segmentsPerWindow,
        ILogger<RedisSlidingWindowRateLimiter>? logger = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("key cannot be null or empty", nameof(key));
        _key = key;
        _permitLimit = permitLimit > 0 ? permitLimit : throw new ArgumentOutOfRangeException(nameof(permitLimit));
        _window = window;
        _segmentsPerWindow = segmentsPerWindow > 0 ? segmentsPerWindow : 1;
        _counter = 0;
        _logger = logger ?? NullLogger<RedisSlidingWindowRateLimiter>.Instance;
    }

    /// <inheritdoc />
    public override RateLimiterStatistics GetStatistics() => new();

    /// <inheritdoc />
    public override TimeSpan? IdleDuration => TimeSpan.Zero;

    /// <inheritdoc />
    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        // 同步调用 Redis 会阻塞线程；RateLimiter 抽象要求同步实现。
        // ASP.NET Core 中间件优先调用 AcquireAsyncCore，此处仅在用户代码同步调用时使用。
        var acquired = TryAcquireSync(permitCount);
        return acquired ? new RedisRateLimitLease(this) : LeaseFailed();
    }

    /// <inheritdoc />
    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        var acquired = await TryAcquireAsync(permitCount, cancellationToken);
        return acquired ? new RedisRateLimitLease(this) : LeaseFailed();
    }

    private bool TryAcquireSync(int permitCount)
    {
        try
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var windowStartMs = nowMs - (long)_window.TotalMilliseconds;
            var member = $"{nowMs}:{Interlocked.Increment(ref _counter)}";
            var ttlSeconds = (int)Math.Ceiling(_window.TotalSeconds * 1.1); // 留 10% 余量避免提前过期

            var result = (long)_database.ScriptEvaluate(
                Script,
                new RedisKey[] { _key },
                new RedisValue[]
                {
                    nowMs,
                    windowStartMs,
                    member,
                    _permitLimit,
                    ttlSeconds
                });

            return result == 1L;
        }
        catch (Exception ex)
        {
            // T28：Redis 不可用时降级放行（fail-open），避免 Redis 故障阻断所有流量。
            // 记录 warning 日志便于运维感知限流器故障，包含异常信息与 Redis key。
            _logger.LogWarning(ex,
                "Redis 滑动窗口限流器同步路径异常，fail-open 放行。Key={Key} PermitLimit={Limit} Window={Window}",
                _key, _permitLimit, _window);
            return true;
        }
    }

    private async ValueTask<bool> TryAcquireAsync(int permitCount, CancellationToken cancellationToken)
    {
        try
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var windowStartMs = nowMs - (long)_window.TotalMilliseconds;
            var member = $"{nowMs}:{Interlocked.Increment(ref _counter)}";
            var ttlSeconds = (int)Math.Ceiling(_window.TotalSeconds * 1.1);

            var result = (long)await _database.ScriptEvaluateAsync(
                Script,
                new RedisKey[] { _key },
                new RedisValue[]
                {
                    nowMs,
                    windowStartMs,
                    member,
                    _permitLimit,
                    ttlSeconds
                });

            return result == 1L;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // T28：Redis 不可用时降级放行（fail-open）。
            // 记录 warning 日志便于运维感知限流器故障，包含异常信息与 Redis key。
            _logger.LogWarning(ex,
                "Redis 滑动窗口限流器异步路径异常，fail-open 放行。Key={Key} PermitLimit={Limit} Window={Window}",
                _key, _permitLimit, _window);
            return true;
        }
    }

    private static FailedLease LeaseFailed() => new FailedLease();

    private sealed class RedisRateLimitLease : RateLimitLease
    {
        private readonly RedisSlidingWindowRateLimiter _limiter;
        public RedisRateLimitLease(RedisSlidingWindowRateLimiter limiter) => _limiter = limiter;
        public override bool IsAcquired => true;
        public override IEnumerable<string> MetadataNames => Array.Empty<string>();
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }

    private sealed class FailedLease : RateLimitLease
    {
        private static readonly string[] MetadataNamesArray = { "REASON" };
        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => MetadataNamesArray;
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == "REASON")
            {
                metadata = "Rate limit exceeded";
                return true;
            }
            metadata = null;
            return false;
        }
    }
}
