using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Order.Infrastructure.Repositories;

/// <summary>
/// 库存仓储 Redis 实现，基于 Lua 脚本保证预占/确认/释放的原子性。
/// 采用"Redis 原子层 + DB 聚合审计源"双写策略：
/// - Redis Lua 脚本保证扣减原子性（高性能）；
/// - 操作成功后加载 StockReservation 聚合根，调用聚合方法维护不变量并发布领域事件，持久化到 DB（审计/对账源）。
/// Redis Key 设计：
/// - inventory:stock:{skuId} — 可用库存（String）
/// - inventory:reserved:{skuId}:{orderId} — 单订单预占数量（String）
/// </summary>
public sealed class RedisInventoryRepository : IInventoryRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IStockReservationRepository _stockReservationRepository;
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

    /// <summary>
    /// Lua 脚本：归还已扣减库存，将已扣减数量加回可用库存。
    /// KEYS[1] = 可用库存 key
    /// ARGV[1] = 归还数量
    /// 返回：1
    /// </summary>
    private const string ReturnDeductedLuaScript = @"
local qty = tonumber(ARGV[1])
redis.call('INCRBY', KEYS[1], qty)
return 1";

    public RedisInventoryRepository(
        IConnectionMultiplexer redis,
        IStockReservationRepository stockReservationRepository,
        ILogger<RedisInventoryRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(stockReservationRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _stockReservationRepository = stockReservationRepository;
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
            // 双写：加载聚合根，调用 ReserveStock 维护不变量并发布领域事件
            await PersistAggregateAsync(
                async () => await _stockReservationRepository.GetOrCreateAsync(skuId, ct),
                reservation => reservation.ReserveStock(orderId, quantity),
                ct);
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

        // 双写：加载聚合根，调用 ConfirmStockDeduction 维护不变量并发布领域事件
        await PersistAggregateAsync(
            async () => await _stockReservationRepository.GetBySkuIdAsync(skuId, ct),
            reservation => reservation.ConfirmStockDeduction(orderId, quantity),
            ct);

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

        // 双写：加载聚合根，调用 ReleaseStock 维护不变量并发布领域事件
        await PersistAggregateAsync(
            async () => await _stockReservationRepository.GetBySkuIdAsync(skuId, ct),
            reservation => reservation.ReleaseStock(orderId, quantity),
            ct);

        _logger.LogInformation("库存预占释放 SkuId={SkuId} OrderId={OrderId} Quantity={Quantity}",
            skuId, orderId, quantity);
    }

    /// <inheritdoc />
    public async Task ReturnDeductedAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);

        await db.ScriptEvaluateAsync(
            ReturnDeductedLuaScript,
            new RedisKey[] { stockKey },
            new RedisValue[] { quantity });

        // 双写：加载聚合根，调用 Replenish 归还已扣减库存
        await PersistAggregateAsync(
            async () => await _stockReservationRepository.GetBySkuIdAsync(skuId, ct),
            reservation => reservation.Replenish(quantity),
            ct);

        _logger.LogInformation("已扣减库存归还 SkuId={SkuId} OrderId={OrderId} Quantity={Quantity}",
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

        // 双写：同步聚合基线
        await PersistAggregateAsync(
            async () => await _stockReservationRepository.GetOrCreateAsync(skuId, ct),
            reservation =>
            {
                var delta = availableQty - reservation.AvailableQty;
                if (delta != 0)
                {
                    reservation.Replenish(delta);
                }
            },
            ct);

        _logger.LogInformation("库存基线同步 SkuId={SkuId} AvailableQty={AvailableQty}", skuId, availableQty);
    }

    /// <summary>
    /// 双写辅助：加载聚合根，执行聚合操作，持久化。Redis 已成功，DB 双写失败仅告警不回滚。
    /// </summary>
    /// <param name="loadAsync">加载聚合根的委托（GetBySkuId 或 GetOrCreate）。</param>
    /// <param name="applyAction">对聚合根执行的操作（如 ReserveStock）。</param>
    /// <param name="ct">取消令牌。</param>
    private async Task PersistAggregateAsync(
        Func<Task<StockReservation?>> loadAsync,
        Action<StockReservation> applyAction,
        CancellationToken ct)
    {
        try
        {
            var reservation = await loadAsync();
            if (reservation is null)
            {
                _logger.LogWarning("库存预占聚合未找到，跳过双写");
                return;
            }

            applyAction(reservation);
            await _stockReservationRepository.UpdateAsync(reservation, ct);
        }
        catch (Exception ex)
        {
            // Redis 已成功提交，DB 双写失败仅告警，不影响主流程
            _logger.LogWarning(ex, "库存预占聚合双写失败（Redis 已成功），需后续对账");
        }
    }

    private static string BuildStockKey(Guid skuId) => $"inventory:stock:{skuId}";
    private static string BuildReservedKey(Guid skuId, Guid orderId) => $"inventory:reserved:{skuId}:{orderId}";
}
