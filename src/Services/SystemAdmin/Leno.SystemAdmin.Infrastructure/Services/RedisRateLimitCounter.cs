using System.Text;
using Leno.SystemAdmin.Domain.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Infrastructure.Services;

/// <summary>
/// Redis 限流计数器实现，基于 Redis Lua 脚本实现原子计数与固定窗口限流。
/// 使用 <c>IConnectionMultiplexer</c> 注入 Redis 连接。
/// Lua 脚本保证「检查+递增」的原子性，避免竞态条件。
/// </summary>
public sealed class RedisRateLimitCounter : IRateLimitCounter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimitCounter> _logger;

    /// <summary>
    /// Redis Lua 脚本：原子地检查并递增计数器。
    /// 如果键不存在，创建并设置过期时间；如果计数未超限，递增并返回 1；否则返回 0。
    /// </summary>
    private const string CheckAndIncrementScript = """
        local current = redis.call('GET', KEYS[1])
        if current == false then
            redis.call('SET', KEYS[1], 1, 'EX', tonumber(ARGV[2]))
            return 1
        end
        if tonumber(current) < tonumber(ARGV[1]) then
            redis.call('INCR', KEYS[1])
            return 1
        end
        return 0
        """;

    private const string RateLimitKeyPrefix = "rate_limit:";

    public RedisRateLimitCounter(IConnectionMultiplexer redis, ILogger<RedisRateLimitCounter> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> CheckAndIncrementAsync(string key, int limit, int windowSeconds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("限流键不可为空", nameof(key));
        }

        if (limit <= 0)
        {
            throw new ArgumentException("限流阈值必须大于 0", nameof(limit));
        }

        if (windowSeconds <= 0)
        {
            throw new ArgumentException("时间窗口必须大于 0", nameof(windowSeconds));
        }

        var redisKey = $"{RateLimitKeyPrefix}{key}";
        var db = _redis.GetDatabase();

        try
        {
            var result = await db.ScriptEvaluateAsync(
                CheckAndIncrementScript,
                new RedisKey[] { redisKey },
                new RedisValue[] { limit, windowSeconds });

            var allowed = (int)result == 1;

            if (!allowed)
            {
                _logger.LogWarning("限流触发：Key={Key}, Limit={Limit}, Window={WindowSeconds}s", key, limit, windowSeconds);
            }

            return allowed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 限流计数器执行失败：Key={Key}", key);
            // 降级策略：Redis 不可用时放行请求
            return true;
        }
    }

    /// <inheritdoc />
    public async Task<long> GetCountAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("限流键不可为空", nameof(key));
        }

        var redisKey = $"{RateLimitKeyPrefix}{key}";
        var db = _redis.GetDatabase();

        try
        {
            var value = await db.StringGetAsync(redisKey);
            return value.IsNull ? 0 : (long)value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 限流计数查询失败：Key={Key}", key);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task ResetAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("限流键不可为空", nameof(key));
        }

        var redisKey = $"{RateLimitKeyPrefix}{key}";
        var db = _redis.GetDatabase();

        try
        {
            await db.KeyDeleteAsync(redisKey);
            _logger.LogDebug("限流计数器已重置：Key={Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 限流计数器重置失败：Key={Key}", key);
        }
    }
}