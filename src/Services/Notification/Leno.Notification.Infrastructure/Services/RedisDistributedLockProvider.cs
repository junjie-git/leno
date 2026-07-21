using Leno.Notification.Domain.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Notification.Infrastructure.Services;

/// <summary>
/// 基于 Redis SET NX EX 模式的分布式锁实现。
/// 释放时使用 Lua 脚本校验令牌，防止误删他人持有的锁。
/// Redis 不可用时降级为允许（fail-open），返回非 null 令牌以避免阻塞 Job 处理。
/// </summary>
public sealed class RedisDistributedLockProvider : IDistributedLockProvider
{
    private const string LockKeyPrefix = "notification:lock:";

    /// <summary>
    /// 释放锁的 Lua 脚本：仅当键值等于令牌时才删除，避免误删他人持有的锁。
    /// </summary>
    private static readonly string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDistributedLockProvider> _logger;

    public RedisDistributedLockProvider(IConnectionMultiplexer redis, ILogger<RedisDistributedLockProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Guid.NewGuid().ToString("N");
        }

        try
        {
            var db = _redis.GetDatabase();
            var token = Guid.NewGuid().ToString("N");
            var fullKey = LockKeyPrefix + key;
            // SET key value NX EX seconds —— 仅当键不存在时设置，并附带过期时间
            var acquired = await db.StringSetAsync(fullKey, token, expiry, When.NotExists);
            return acquired ? token : null;
        }
        catch (Exception ex)
        {
            // Redis 不可用时降级为允许（fail-open），避免阻塞所有 Job 处理。
            // 此时多实例可能重复处理，由幂等检查与状态机校验兜底。
            _logger.LogError(ex, "Redis 分布式锁获取失败，降级为允许 Key={Key}", key);
            return Guid.NewGuid().ToString("N");
        }
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(string key, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            var fullKey = LockKeyPrefix + key;
            // 使用 Lua 脚本确保只删除自己持有的锁（token 匹配）
            await db.ScriptEvaluateAsync(ReleaseScript, new RedisKey[] { fullKey }, new RedisValue[] { token });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 分布式锁释放失败 Key={Key}", key);
        }
    }
}
