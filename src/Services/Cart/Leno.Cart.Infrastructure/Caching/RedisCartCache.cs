using System.Text.Json;
using Leno.Cart.Domain.Aggregates;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Caching;

/// <summary>
/// 购物车 Redis 缓存，采用 Hash 存储购物车，读写穿透策略，TTL 7 天。
/// 供应用层读侧加速，写侧仍以 EF Core 持久化为主。
/// </summary>
public sealed class RedisCartCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCartCache> _logger;

    public RedisCartCache(IConnectionMultiplexer redis, ILogger<RedisCartCache> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <summary>按买家标识读取购物车缓存。</summary>
    public async Task<CartAggregate?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(userId);
            var value = await db.StringGetAsync(key);
            if (!value.HasValue)
            {
                return null;
            }

            return JsonSerializer.Deserialize<CartAggregate>((string)value!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取购物车缓存失败 UserId={UserId}", userId);
            return null;
        }
    }

    /// <summary>写入购物车缓存并刷新 TTL。</summary>
    public async Task SetAsync(CartAggregate cart, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cart);
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(cart.UserId);
            var value = JsonSerializer.Serialize(cart, JsonOptions);
            await db.StringSetAsync(key, value, Ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入购物车缓存失败 UserId={UserId}", cart.UserId);
        }
    }

    /// <summary>按买家标识删除购物车缓存。</summary>
    public async Task RemoveAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(userId);
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除购物车缓存失败 UserId={UserId}", userId);
        }
    }

    private static string BuildKey(Guid userId) => $"cart:{userId}";
}
