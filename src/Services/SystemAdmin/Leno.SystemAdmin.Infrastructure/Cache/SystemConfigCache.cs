using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Infrastructure.Cache;

/// <summary>
/// 系统配置 Redis 缓存，读写穿透策略，TTL 30 分钟。
/// 供应用层读侧加速，缓存缺失时回源数据库读取；写侧以 EF Core 持久化为主。
/// </summary>
public sealed class SystemConfigCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SystemConfigCache> _logger;

    public SystemConfigCache(IConnectionMultiplexer redis, ILogger<SystemConfigCache> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <summary>按配置键读取缓存值，缓存缺失或异常返回 null（视为缓存未命中）。</summary>
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(BuildKey(key));
            return value.HasValue ? (string?)value : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取系统配置缓存失败 Key={Key}", key);
            return null;
        }
    }

    /// <summary>写入配置缓存并刷新 TTL。</summary>
    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(BuildKey(key), value, Ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入系统配置缓存失败 Key={Key}", key);
        }
    }

    /// <summary>按配置键删除缓存。</summary>
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(BuildKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除系统配置缓存失败 Key={Key}", key);
        }
    }

    private static string BuildKey(string key) => $"sys:config:{key}";
}
