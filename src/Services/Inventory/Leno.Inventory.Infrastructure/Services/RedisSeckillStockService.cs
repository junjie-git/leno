using Leno.Inventory.Domain.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Inventory.Infrastructure.Services;

/// <summary>
/// 秒杀库存 Redis 预扣服务实现，基于 Hash 结构支持多 SKU，Lua 脚本保证原子性。
/// 迁移自 Promotion BC（新代码，基于计划 §4.1.1 结构），Promotion BC 旧实现保留不动，
/// 秒杀库存调用方迁移为遗留项，待 Promotion 规则引擎任务完成后单独迁移。
/// Redis Key 设计：
/// - seckill:{activityId}:stock — Hash，field = skuId，value = 剩余库存（随预扣递减、回退递增）
/// - seckill:{activityId}:total  — Hash，field = skuId，value = 初始/总库存基线（回退上限保护，初始化时与 stock 相同）
/// - seckill:user:{activityId}:{skuId}:{userId} — String，用户已购数量
/// </summary>
/// <remarks>
/// 与 Promotion BC 旧实现的差异：
/// 1. 旧实现 <c>RestoreAsync</c> 通过 <c>ISeckillActivityRepository</c> 查询 TotalStock 作为回退上限；
///    本实现不依赖 Promotion BC 仓储，改为在 <see cref="InitializeAsync"/> 时将初始库存写入
///    <c>seckill:{activityId}:total</c> Hash，<see cref="RestoreAsync"/> 时从该 Hash 读取上限。
/// 2. 旧实现包含 <c>WriteBackToDbAsync</c>（活动结束回写 DB），该方法依赖 Promotion BC 的
///    SeckillActivity 聚合，Inventory BC 不持有该聚合，故本接口不包含此方法。
///    活动结束时的 Redis→DB 回写仍由 Promotion BC 旧实现负责（遗留项），待调用方迁移后统一处理。
/// </remarks>
public sealed class RedisSeckillStockService : ISeckillStockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisSeckillStockService> _logger;

    /// <summary>
    /// Lua 脚本：原子校验并扣减指定 SKU 库存 + 累加用户已购数量。
    /// KEYS[1] = 库存 Hash key, KEYS[2] = 用户已购 key
    /// ARGV[1] = skuId (field), ARGV[2] = 本次数量, ARGV[3] = 限购上限
    /// 返回：0=成功，1=库存不足（或库存 key 不存在），2=超限购
    /// </summary>
    private const string DeductLuaScript = @"
local stock = tonumber(redis.call('HGET', KEYS[1], ARGV[1]))
if stock == nil then return 1 end
local qty = tonumber(ARGV[2])
if stock < qty then return 1 end
local bought = tonumber(redis.call('GET', KEYS[2]) or '0')
local limit = tonumber(ARGV[3])
if bought + qty > limit then return 2 end
redis.call('HINCRBY', KEYS[1], ARGV[1], -qty)
redis.call('INCRBY', KEYS[2], qty)
return 0";

    /// <summary>
    /// Lua 脚本：原子回退指定 SKU 库存，带 TotalStock 上限保护（从 total Hash 读取，防双重复回退导致库存膨胀）。
    /// KEYS[1] = 库存 Hash key, KEYS[2] = 总库存 Hash key
    /// ARGV[1] = skuId (field), ARGV[2] = 回退数量
    /// 返回：0=成功，1=回退后超出 TotalStock 上限（已拒绝回退，防库存膨胀）
    /// </summary>
    private const string RestoreLuaScript = @"
local cur = tonumber(redis.call('HGET', KEYS[1], ARGV[1]) or '0')
local total = tonumber(redis.call('HGET', KEYS[2], ARGV[1]) or '0')
local qty = tonumber(ARGV[2])
local new = cur + qty
if total > 0 and new > total then return 1 end
redis.call('HINCRBY', KEYS[1], ARGV[1], qty)
return 0";

    public RedisSeckillStockService(
        IConnectionMultiplexer redis,
        ILogger<RedisSeckillStockService> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(Guid activityId, Dictionary<Guid, int> skuStocks, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skuStocks);
        if (activityId == Guid.Empty)
        {
            throw new ArgumentException("ActivityId 不可为空", nameof(activityId));
        }
        if (skuStocks.Count == 0)
        {
            _logger.LogWarning("秒杀活动 {ActivityId} 库存初始化明细为空，跳过", activityId);
            return;
        }

        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);
        var totalKey = BuildTotalKey(activityId);

        // 校验每个 SKU 的库存数量合法，过滤非正值
        var entries = new List<HashEntry>(skuStocks.Count);
        foreach (var (skuId, qty) in skuStocks)
        {
            if (qty < 0)
            {
                _logger.LogWarning("秒杀活动 {ActivityId} SkuId={SkuId} 初始库存为负 {Qty}，跳过该 SKU", activityId, skuId, qty);
                continue;
            }
            entries.Add(new HashEntry(skuId.ToString(), qty));
        }

        if (entries.Count == 0)
        {
            _logger.LogWarning("秒杀活动 {ActivityId} 库存初始化后无有效 SKU，跳过", activityId);
            return;
        }

        // 库存 Hash 与总库存 Hash 同时初始化为相同值（总库存作为回退上限保护基线）
        await db.HashSetAsync(stockKey, entries.ToArray());
        await db.HashSetAsync(totalKey, entries.ToArray());

        _logger.LogInformation(
            "秒杀活动 {ActivityId} Redis 多 SKU 库存初始化完成，SKU 数量：{Count}",
            activityId, entries.Count);
    }

    /// <inheritdoc />
    public async Task<int> TryDeductAsync(
        Guid activityId,
        Guid skuId,
        Guid userId,
        int quantity,
        int limitPerUser,
        CancellationToken ct = default)
    {
        if (activityId == Guid.Empty)
        {
            throw new ArgumentException("ActivityId 不可为空", nameof(activityId));
        }
        if (skuId == Guid.Empty)
        {
            throw new ArgumentException("SkuId 不可为空", nameof(skuId));
        }
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }
        if (quantity <= 0)
        {
            throw new ArgumentException("本次下单数量须大于 0", nameof(quantity));
        }
        if (limitPerUser <= 0)
        {
            throw new ArgumentException("每人限购上限须大于 0", nameof(limitPerUser));
        }

        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);
        var userKey = BuildUserKey(activityId, skuId, userId);

        var result = (long?)await db.ScriptEvaluateAsync(
            DeductLuaScript,
            new RedisKey[] { stockKey, userKey },
            new RedisValue[] { skuId.ToString(), quantity, limitPerUser });

        var code = result ?? -1;
        if (code == 0)
        {
            _logger.LogInformation(
                "秒杀预扣成功 ActivityId={ActivityId} SkuId={SkuId} UserId={UserId} Quantity={Quantity} Limit={Limit}",
                activityId, skuId, userId, quantity, limitPerUser);
            return 0;
        }

        _logger.LogInformation(
            "秒杀预扣失败 ActivityId={ActivityId} SkuId={SkuId} UserId={UserId} Code={Code}（1=库存不足，2=超限购）",
            activityId, skuId, userId, code);
        return (int)code;
    }

    /// <inheritdoc />
    public async Task RestoreAsync(Guid activityId, Guid skuId, int quantity, CancellationToken ct = default)
    {
        if (activityId == Guid.Empty)
        {
            throw new ArgumentException("ActivityId 不可为空", nameof(activityId));
        }
        if (skuId == Guid.Empty)
        {
            throw new ArgumentException("SkuId 不可为空", nameof(skuId));
        }
        if (quantity <= 0)
        {
            throw new ArgumentException("回退数量须大于 0", nameof(quantity));
        }

        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);
        var totalKey = BuildTotalKey(activityId);

        var result = (long?)await db.ScriptEvaluateAsync(
            RestoreLuaScript,
            new RedisKey[] { stockKey, totalKey },
            new RedisValue[] { skuId.ToString(), quantity });

        var code = result ?? -1;
        if (code == 1)
        {
            // 回退后超出 TotalStock 上限：记日志但不抛异常（防回退风暴），调用方按业务正常完成处理
            _logger.LogWarning(
                "秒杀库存回退超出 TotalStock 上限，已拒绝回退 ActivityId={ActivityId} SkuId={SkuId} Quantity={Quantity}",
                activityId, skuId, quantity);
            return;
        }

        _logger.LogInformation(
            "秒杀库存回退 ActivityId={ActivityId} SkuId={SkuId} Quantity={Quantity}",
            activityId, skuId, quantity);
    }

    /// <inheritdoc />
    public async Task<int> GetAvailableAsync(Guid activityId, Guid skuId, CancellationToken ct = default)
    {
        if (activityId == Guid.Empty)
        {
            throw new ArgumentException("ActivityId 不可为空", nameof(activityId));
        }
        if (skuId == Guid.Empty)
        {
            throw new ArgumentException("SkuId 不可为空", nameof(skuId));
        }

        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);
        var value = await db.HashGetAsync(stockKey, skuId.ToString());
        return (int?)value ?? 0;
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, int>> GetAllStocksAsync(Guid activityId, CancellationToken ct = default)
    {
        if (activityId == Guid.Empty)
        {
            throw new ArgumentException("ActivityId 不可为空", nameof(activityId));
        }

        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);
        var entries = await db.HashGetAllAsync(stockKey);

        var result = new Dictionary<Guid, int>(entries.Length);
        foreach (var entry in entries)
        {
            if (Guid.TryParse(entry.Name.ToString(), out var skuId))
            {
                result[skuId] = (int?)entry.Value ?? 0;
            }
        }
        return result;
    }

    private static string BuildStockKey(Guid activityId) => $"seckill:{activityId}:stock";
    private static string BuildTotalKey(Guid activityId) => $"seckill:{activityId}:total";
    private static string BuildUserKey(Guid activityId, Guid skuId, Guid userId) =>
        $"seckill:user:{activityId}:{skuId}:{userId}";
}
