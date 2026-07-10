using Leno.Promotion.Domain.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Promotion.Infrastructure.Services;

/// <summary>
/// 秒杀库存 Redis 预扣服务实现，基于 Lua 脚本保证"扣减库存 + 校验限购"原子性。
/// Redis Key 设计：
/// - seckill:stock:{activityId} — 剩余库存（String）
/// - seckill:user:{activityId}:{userId} — 用户已购数量（String）
/// </summary>
public sealed class RedisSeckillStockService : ISeckillStockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisSeckillStockService> _logger;

    /// <summary>
    /// Lua 脚本：原子校验并扣减库存 + 累加用户已购数量。
    /// KEYS[1] = 库存 key, KEYS[2] = 用户已购 key
    /// ARGV[1] = 本次数量, ARGV[2] = 限购上限
    /// 返回：0=成功，1=库存不足，2=超限购
    /// </summary>
    private const string DeductLuaScript = @"
local stock = tonumber(redis.call('GET', KEYS[1]))
if stock == nil then return 1 end
local qty = tonumber(ARGV[1])
if stock < qty then return 1 end
local bought = tonumber(redis.call('GET', KEYS[2]) or '0')
local limit = tonumber(ARGV[2])
if bought + qty > limit then return 2 end
redis.call('DECRBY', KEYS[1], qty)
redis.call('INCRBY', KEYS[2], qty)
return 0";

    /// <summary>
    /// Lua 脚本：原子回退库存 + 扣减用户已购数量。
    /// </summary>
    private const string RestoreLuaScript = @"
local qty = tonumber(ARGV[1])
redis.call('INCRBY', KEYS[1], qty)
local bought = tonumber(redis.call('GET', KEYS[2]) or '0')
if bought >= qty then
    redis.call('DECRBY', KEYS[2], qty)
else
    redis.call('SET', KEYS[2], '0')
end
return 0";

    public RedisSeckillStockService(IConnectionMultiplexer redis, ILogger<RedisSeckillStockService> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(Guid activityId, int totalStock, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);
        await db.StringSetAsync(stockKey, totalStock);
        _logger.LogInformation("秒杀活动 {ActivityId} Redis 库存初始化为 {TotalStock}", activityId, totalStock);
    }

    /// <inheritdoc />
    public async Task<bool> TryDeductAsync(
        Guid activityId,
        Guid userId,
        int quantity,
        int limitPerUser,
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);
        var userKey = BuildUserKey(activityId, userId);

        var result = (long?)await db.ScriptEvaluateAsync(
            DeductLuaScript,
            new RedisKey[] { stockKey, userKey },
            new RedisValue[] { quantity, limitPerUser });

        var code = result ?? -1;
        if (code == 0)
        {
            _logger.LogInformation("秒杀预扣成功 ActivityId={ActivityId} UserId={UserId} Quantity={Quantity}",
                activityId, userId, quantity);
            return true;
        }

        _logger.LogInformation("秒杀预扣失败 ActivityId={ActivityId} UserId={UserId} Code={Code}（1=库存不足，2=超限购）",
            activityId, userId, code);
        return false;
    }

    /// <inheritdoc />
    public async Task RestoreAsync(Guid activityId, Guid userId, int quantity, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);
        var userKey = BuildUserKey(activityId, userId);

        await db.ScriptEvaluateAsync(
            RestoreLuaScript,
            new RedisKey[] { stockKey, userKey },
            new RedisValue[] { quantity });

        _logger.LogInformation("秒杀库存回退 ActivityId={ActivityId} UserId={UserId} Quantity={Quantity}",
            activityId, userId, quantity);
    }

    /// <inheritdoc />
    public async Task<int> GetAvailableAsync(Guid activityId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(activityId);
        var value = await db.StringGetAsync(stockKey);
        return (int?)value ?? 0;
    }

    private static string BuildStockKey(Guid activityId) => $"seckill:stock:{activityId}";
    private static string BuildUserKey(Guid activityId, Guid userId) => $"seckill:user:{activityId}:{userId}";
}
