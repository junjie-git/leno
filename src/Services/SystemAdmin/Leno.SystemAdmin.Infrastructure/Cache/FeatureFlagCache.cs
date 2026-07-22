using Leno.SystemAdmin.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Infrastructure.Cache;

/// <summary>
/// 特性开关 Redis 缓存，读写穿透策略，TTL 30 分钟。
/// 供应用层读侧加速，缓存缺失时回源数据库读取；写侧以 EF Core 持久化为主。
/// </summary>
public sealed class FeatureFlagCache : IFeatureFlagCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<FeatureFlagCache> _logger;

    public FeatureFlagCache(IConnectionMultiplexer redis, ILogger<FeatureFlagCache> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <summary>按开关键读取缓存值，缓存缺失或异常返回 null（视为缓存未命中）。</summary>
    public async Task<string?> GetAsync(string flagKey, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(BuildKey(flagKey));
            return value.HasValue ? (string?)value : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取特性开关缓存失败 Key={FlagKey}", flagKey);
            return null;
        }
    }

    /// <summary>写入开关缓存并刷新 TTL。</summary>
    public async Task SetAsync(string flagKey, string value, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(BuildKey(flagKey), value, Ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入特性开关缓存失败 Key={FlagKey}", flagKey);
        }
    }

    /// <summary>按开关键删除缓存。</summary>
    public async Task RemoveAsync(string flagKey, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(BuildKey(flagKey));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除特性开关缓存失败 Key={FlagKey}", flagKey);
        }
    }

    private static string BuildKey(string flagKey) => $"sys:flag:{flagKey}";
}
