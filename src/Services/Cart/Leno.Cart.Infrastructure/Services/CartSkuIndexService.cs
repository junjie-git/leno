using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 基于 Redis Set 的购物车-SKU 反向索引实现。
/// Key 格式：cart:sku:{skuId}，Value：购物车 ID 集合（Set 成员）。
/// 由 Cart 聚合领域事件（SkuAddedToCartEvent/SkuRemovedFromCartEvent）驱动维护。
/// </summary>
/// <remarks>
/// P1-5：每次 <see cref="AddAsync"/> 后刷新 Key TTL 为 30 天，避免购物车被删除后 stale 索引永久驻留。
/// P1-6：基础设施故障统一包装为 <see cref="CartInfrastructureException"/> 上抛，与
/// <c>RedisAnonymousCartRepository</c> 异常处理策略一致，触发消费者重试与死信兜底。
/// </remarks>
public sealed class CartSkuIndexService : ICartSkuIndexService
{
    private const string KeyPrefix = "cart:sku:";
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(30);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CartSkuIndexService> _logger;

    public CartSkuIndexService(IConnectionMultiplexer redis, ILogger<CartSkuIndexService> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddAsync(Guid skuId, Guid cartId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = $"{KeyPrefix}{skuId}";
            await db.SetAddAsync(key, cartId.ToString());
            // P1-5：每次 Add 刷新 TTL，避免购物车删除后 stale 索引永久驻留
            await db.KeyExpireAsync(key, Ttl);
        }
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "维护购物车-SKU 反向索引失败（Add） SkuId={SkuId} CartId={CartId}", skuId, cartId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Guid skuId, Guid cartId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.SetRemoveAsync($"{KeyPrefix}{skuId}", cartId.ToString());
        }
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "维护购物车-SKU 反向索引失败（Remove） SkuId={SkuId} CartId={CartId}", skuId, cartId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
        }
    }

    /// <inheritdoc />
    public async Task<List<Guid>> GetCartIdsBySkuAsync(Guid skuId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var values = await db.SetMembersAsync($"{KeyPrefix}{skuId}");
            return values
                .Select(v => Guid.TryParse((string)v!, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();
        }
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "查询购物车-SKU 反向索引失败 SkuId={SkuId}", skuId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
        }
    }
}
