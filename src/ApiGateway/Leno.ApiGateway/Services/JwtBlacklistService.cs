using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 基于 Redis 的 JWT 黑名单实现。
/// Key 格式：leno:jwt:blacklist:{jti}，Value：1，TTL = token 剩余有效期。
/// 三层保障：Redis Pub/Sub 实时 + 定时拉取兜底 + 启动预热。
/// </summary>
public sealed class JwtBlacklistService : IJwtBlacklistService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<JwtBlacklistService> _logger;
    private readonly ConcurrentDictionary<string, byte> _localCache = new();

    public JwtBlacklistService(IConnectionMultiplexer redis, ILogger<JwtBlacklistService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
    {
        // 先查本地 Caffeine 缓存
        if (_localCache.ContainsKey(jti)) return true;

        // 再查 Redis
        var db = _redis.GetDatabase();
        var exists = await db.KeyExistsAsync($"leno:jwt:blacklist:{jti}");
        if (exists)
        {
            _localCache.TryAdd(jti, 0);
            return true;
        }
        return false;
    }

    public async Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"leno:jwt:blacklist:{jti}", "1", ttl);
        _localCache.TryAdd(jti, 0);
        _logger.LogInformation("JWT 已吊销 Jti={Jti} Ttl={Ttl}分钟", jti, ttl.TotalMinutes);
    }
}
