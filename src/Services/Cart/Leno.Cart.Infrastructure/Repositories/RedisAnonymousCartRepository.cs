using System.Text.Json;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Repositories;

/// <summary>
/// 匿名购物车 Redis 仓储实现，以会话标识为键存储匿名购物车聚合。
/// TTL 7 天，每次操作刷新过期时间。
/// </summary>
public sealed class RedisAnonymousCartRepository : IAnonymousCartRepository
{
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisAnonymousCartRepository> _logger;

    public RedisAnonymousCartRepository(IConnectionMultiplexer redis, ILogger<RedisAnonymousCartRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CartAggregate?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            var value = await db.StringGetAsync(key);
            if (!value.HasValue)
            {
                return null;
            }

            return JsonSerializer.Deserialize<CartAggregate>((string)value!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取匿名购物车缓存失败 SessionId={SessionId}", sessionId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(string sessionId, CartAggregate cart, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(cart);
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            var value = JsonSerializer.Serialize(cart, JsonOptions);
            await db.StringSetAsync(key, value, Ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入匿名购物车缓存失败 SessionId={SessionId}", sessionId);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除匿名购物车缓存失败 SessionId={SessionId}", sessionId);
        }
    }

    /// <inheritdoc />
    public async Task RefreshTtlAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            await db.KeyExpireAsync(key, Ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "刷新匿名购物车 TTL 失败 SessionId={SessionId}", sessionId);
        }
    }

    private static string BuildKey(string sessionId) => $"cart:anon:{sessionId}";
}