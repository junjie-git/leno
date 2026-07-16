using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using SeckillActivityAggregate = Leno.Promotion.Domain.Aggregates.SeckillActivity;

namespace Leno.Promotion.Infrastructure.Services;

/// <summary>
/// 秒杀库存 Redis 预扣服务实现，基于 Hash 结构支持多 SKU，Lua 脚本保证原子性。
/// Redis Key 设计：
/// - seckill:{activityId}:stock — Hash，field = skuId，value = 剩余库存
/// - seckill:user:{activityId}:{skuId}:{userId} — String，用户已购数量
/// </summary>
public sealed class RedisSeckillStockService : ISeckillStockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ISeckillActivityRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RedisSeckillStockService> _logger;

    /// <summary>
    /// Lua 脚本：原子校验并扣减指定 SKU 库存 + 累加用户已购数量。
    /// KEYS[1] = 库存 Hash key, KEYS[2] = 用户已购 key
    /// ARGV[1] = skuId (field), ARGV[2] = 本次数量, ARGV[3] = 限购上限
    /// 返回：0=成功，1=库存不足，2=超限购
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
    /// Lua 脚本：原子回退指定 SKU 库存。
    /// KEYS[1] = 库存 Hash key
    /// ARGV[1] = skuId (field), ARGV[2] = 回退数量
    /// </summary>
    private const string RestoreLuaScript = @"
local qty = tonumber(ARGV[2])
redis.call('HINCRBY', KEYS[1], ARGV[1], qty)
return 0";

    public RedisSeckillStockService(
        IConnectionMultiplexer redis,
        ISeckillActivityRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<RedisSeckillStockService> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(Guid activityId, Dictionary<Guid, int> skuStocks, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skuStocks);

        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);

        var entries = skuStocks.Select(kv => new HashEntry(kv.Key.ToString(), kv.Value)).ToArray();
        await db.HashSetAsync(stockKey, entries);

        _logger.LogInformation("秒杀活动 {ActivityId} Redis 多 SKU 库存初始化完成，SKU 数量：{Count}", activityId, skuStocks.Count);
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
            _logger.LogInformation("秒杀预扣成功 ActivityId={ActivityId} SkuId={SkuId} UserId={UserId} Quantity={Quantity}",
                activityId, skuId, userId, quantity);
            return 0;
        }

        _logger.LogInformation("秒杀预扣失败 ActivityId={ActivityId} SkuId={SkuId} UserId={UserId} Code={Code}（1=库存不足，2=超限购）",
            activityId, skuId, userId, code);
        return (int)code;
    }

    /// <inheritdoc />
    public async Task RestoreAsync(Guid activityId, Guid skuId, int quantity, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);

        await db.ScriptEvaluateAsync(
            RestoreLuaScript,
            new RedisKey[] { stockKey },
            new RedisValue[] { skuId.ToString(), quantity });

        _logger.LogInformation("秒杀库存回退 ActivityId={ActivityId} SkuId={SkuId} Quantity={Quantity}",
            activityId, skuId, quantity);
    }

    /// <inheritdoc />
    public async Task<int> GetAvailableAsync(Guid activityId, Guid skuId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);
        var value = await db.HashGetAsync(stockKey, skuId.ToString());
        return (int?)value ?? 0;
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, int>> GetAllStocksAsync(Guid activityId, CancellationToken ct = default)
    {
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

    /// <inheritdoc />
    public async Task WriteBackToDbAsync(Guid activityId, CancellationToken ct = default)
    {
        var allStocks = await GetAllStocksAsync(activityId, ct);

        foreach (var (skuId, remainingStock) in allStocks)
        {
            // 通过 SKU 查询进行中的活动，更新库存基线
            var activity = await _repository.GetActiveBySkuIdAsync(skuId, DateTime.UtcNow, ct);
            if (activity is null)
            {
                _logger.LogWarning("WriteBackToDb: SKU {SkuId} 未找到进行中的活动", skuId);
                continue;
            }

            // 以 Redis 剩余库存同步 DB 基线（聚合内仅当 Redis < DB 时更新，避免并发回写覆盖）
            var before = activity.AvailableStock;
            activity.SyncFromRedis(remainingStock);

            if (activity.AvailableStock != before)
            {
                _logger.LogInformation(
                    "WriteBackToDb: ActivityId={ActivityId} SkuId={SkuId} DB 库存由 {Before} 同步为 {After}（Redis={Redis}）",
                    activityId, skuId, before, activity.AvailableStock, remainingStock);
            }
        }

        // 经 UnitOfWork 保存聚合变更与发件箱事件（EF Core 乐观锁由聚合并发标记列保障）
        await _unitOfWork.SaveEntitiesAsync(ct);
        _logger.LogInformation("秒杀活动 {ActivityId} Redis 库存已回写 DB", activityId);
    }

    private static string BuildStockKey(Guid activityId) => $"seckill:{activityId}:stock";
    private static string BuildUserKey(Guid activityId, Guid skuId, Guid userId) => $"seckill:user:{activityId}:{skuId}:{userId}";
}