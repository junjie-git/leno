using Leno.Cart.Domain.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 基于 Redis Set 的购物车-SKU 反向索引实现。
/// Key 格式：cart:sku:{skuId}，Value：购物车 ID 集合（Set 成员）。
/// 由 Cart 聚合领域事件（SkuAddedToCartEvent/SkuRemovedFromCartEvent）驱动维护。
/// </summary>
public sealed class CartSkuIndexService : ICartSkuIndexService
{
    private const string KeyPrefix = "cart:sku:";
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
        var db = _redis.GetDatabase();
        await db.SetAddAsync($"{KeyPrefix}{skuId}", cartId.ToString());
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Guid skuId, Guid cartId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.SetRemoveAsync($"{KeyPrefix}{skuId}", cartId.ToString());
    }

    /// <inheritdoc />
    public async Task<List<Guid>> GetCartIdsBySkuAsync(Guid skuId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var values = await db.SetMembersAsync($"{KeyPrefix}{skuId}");
        return values
            .Select(v => Guid.TryParse((string)v!, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }
}
