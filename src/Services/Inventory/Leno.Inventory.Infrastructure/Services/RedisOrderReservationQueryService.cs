using Leno.Inventory.Application.Services;
using Leno.Inventory.Infrastructure.Repositories;
using Leno.SharedContracts.Integration.Inventory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Inventory.Infrastructure.Services;

/// <summary>
/// 订单库存预占明细查询服务 Redis 实现。
/// 基于 Redis SCAN 模式匹配 <c>inventory:reserved:*:{orderId}</c>，解析出 SkuId 与预占数量。
/// 用于 ConfirmStockCommand / ReleaseStockCommand（不携带 SKU 明细）时获取订单全部预占明细。
/// </summary>
public sealed class RedisOrderReservationQueryService : IOrderReservationQueryService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisOrderReservationQueryService> _logger;

    public RedisOrderReservationQueryService(
        IConnectionMultiplexer redis,
        ILogger<RedisOrderReservationQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReserveStockItem>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            return Array.Empty<ReserveStockItem>();
        }

        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var pattern = $"inventory:reserved:*:{orderId}";
        var items = new List<ReserveStockItem>();

        await foreach (var key in server.KeysAsync(pattern: pattern))
                {
            ct.ThrowIfCancellationRequested();
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (!value.HasValue)
            {
                continue;
            }

            var quantity = (int?)value ?? 0;
            if (quantity <= 0)
            {
                continue;
            }

            var skuId = TryExtractSkuId(key.ToString(), orderId);
            if (skuId is null)
            {
                _logger.LogWarning("解析预占 key 失败 Key={Key}", key);
                continue;
            }

            // SellerId 无法从预占 key 反推，此处填 0；Inventory BC 内部确认/释放不依赖 SellerId
            items.Add(new ReserveStockItem(skuId.Value, quantity, SellerId: 0));
        }

        return items;
    }

    /// <summary>
    /// 从预占 key（<c>inventory:reserved:{skuId}:{orderId}</c>）解析出 SkuId。
    /// </summary>
    /// <param name="key">Redis key 字符串。</param>
    /// <param name="orderId">关联订单标识，用于校验。</param>
    /// <returns>解析出的 SkuId；解析失败返回 null。</returns>
    private static Guid? TryExtractSkuId(string key, Guid orderId)
    {
        // key 格式：inventory:reserved:{skuId}:{orderId}
        var parts = key.Split(':');
        if (parts.Length != 4)
        {
            return null;
        }

        if (!Guid.TryParse(parts[2], out var skuId))
        {
            return null;
        }

        if (!Guid.TryParse(parts[3], out var parsedOrderId) || parsedOrderId != orderId)
        {
            return null;
        }

        return skuId;
    }
}
