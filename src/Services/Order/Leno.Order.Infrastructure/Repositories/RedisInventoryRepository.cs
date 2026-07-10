using Leno.Order.Domain.Repositories;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Order.Infrastructure.Repositories;

/// <summary>
/// 库存仓储 Redis 实现，基于 Lua 脚本保证预占/确认/释放的原子性。
/// Redis Key 设计：
/// - inventory:stock:{skuId} — 可用库存（String）
/// - inventory:reserved:{skuId}:{orderId} — 单订单预占数量（String）
/// </summary>
public sealed class RedisInventoryRepository : IInventoryRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisInventoryRepository> _logger;

    /// <summary>
    /// Lua 脚本：原子校验可用库存充足并预占。
    /// KEYS[1] = 可用库存 key, KEYS[2] = 预占 key
    /// ARGV[1] = 预占数量
    /// 返回：1=成功，0=库存不足或 key 不存在
    /// </summary>
    private const string ReserveLuaScript = @"
local available = tonumber(redis.call('GET', KEYS[1]))
if available == nil then return 0 end
local qty = tonumber(ARGV[1])
if available < qty then return 0 end
redis.call('DECRBY', KEYS[1], qty)
redis.call('SET', KEYS[2], qty)
return 1";

    /// <summary>
    /// Lua 脚本：释放预占库存，将预占数量归还可用库存并删除预占 key。
    /// KEYS[1] = 可用库存 key, KEYS[2] = 预占 key
    /// 返回：1
    /// </summary>
    private const string ReleaseLuaScript = @"
local reserved = tonumber(redis.call('GET', KEYS[2]) or '0')
if reserved == 0 then return 1 end
redis.call('INCRBY', KEYS[1], reserved)
redis.call('DEL', KEYS[2])
return 1";

    /// <summary>
    /// Lua 脚本：确认扣减库存，删除预占 key（预占已转为真实扣减）。
    /// KEYS[2] = 预占 key
    /// 返回：1
    /// </summary>
    private const string ConfirmLuaScript = @"
local reserved = tonumber(redis.call('GET', KEYS[2]) or '0')
if reserved == 0 then return 1 end
redis.call('DEL', KEYS[2])
return 1";

    public RedisInventoryRepository(IConnectionMultiplexer redis, ILogger<RedisInventoryRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ReserveAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);
        var reservedKey = BuildReservedKey(skuId, orderId);

        var result = (long?)await db.ScriptEvaluateAsync(
            ReserveLuaScript,
            new RedisKey[] { stockKey, reservedKey },
            new RedisValue[] { quantity });

        var success = result == 1;
        if (success)
        {
            _logger.LogInformation("库存预占成功 SkuId={SkuId} OrderId={OrderId} Quantity={Quantity}",
                skuId, orderId, quantity);
        }
        else
        {
            _logger.LogInformation("库存预占失败（库存不足）SkuId={SkuId} OrderId={OrderId} Quantity={Quantity}",
                skuId, orderId, quantity);
        }

        return success;
    }

    /// <inheritdoc />
    public async Task ConfirmAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);
        var reservedKey = BuildReservedKey(skuId, orderId);

        await db.ScriptEvaluateAsync(
            ConfirmLuaScript,
            new RedisKey[] { stockKey, reservedKey });

        _logger.LogInformation("库存确认扣减 SkuId={SkuId} OrderId={OrderId} Quantity={Quantity}",
            skuId, orderId, quantity);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);
        var reservedKey = BuildReservedKey(skuId, orderId);

        await db.ScriptEvaluateAsync(
            ReleaseLuaScript,
            new RedisKey[] { stockKey, reservedKey });

        _logger.LogInformation("库存预占释放 SkuId={SkuId} OrderId={OrderId} Quantity={Quantity}",
            skuId, orderId, quantity);
    }

    /// <inheritdoc />
    public async Task<int> GetAvailableAsync(Guid skuId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);
        var value = await db.StringGetAsync(stockKey);
        return (int?)value ?? 0;
    }

    /// <inheritdoc />
    public async Task SetBaseLineAsync(Guid skuId, int availableQty, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);
        await db.StringSetAsync(stockKey, availableQty);
        _logger.LogInformation("库存基线同步 SkuId={SkuId} AvailableQty={AvailableQty}", skuId, availableQty);
    }

    private static string BuildStockKey(Guid skuId) => $"inventory:stock:{skuId}";
    private static string BuildReservedKey(Guid skuId, Guid orderId) => $"inventory:reserved:{skuId}:{orderId}";
}
