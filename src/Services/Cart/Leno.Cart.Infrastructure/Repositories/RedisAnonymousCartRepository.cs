using System.Text.Json;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Repositories;

/// <summary>
/// 匿名购物车 Redis 仓储实现，以会话标识为键存储匿名购物车聚合。
/// TTL 7 天，每次操作刷新过期时间。
/// 基础设施故障（Redis 不可达、超时等）包装为 <see cref="CartInfrastructureException"/> 向上抛出，
/// 避免调用方误判"购物车不存在"并覆盖写入。
/// <para>
/// P1-1 修复：使用 Redis Hash 存储格式 + Lua 脚本实现 CAS（Compare-And-Swap）原子更新，
/// 避免并发场景下覆盖写丢失更新。Hash 格式包含 <c>payload</c>（Cart JSON）与 <c>version</c>（乐观锁版本号）两个字段。
/// </para>
/// </summary>
public sealed class RedisAnonymousCartRepository : IAnonymousCartRepository
{
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

    /// <summary>
    /// JSON 序列化选项，基于 Web 默认配置（camelCase）。
    /// P1-1：<c>AggregateRoot.DomainEvents</c> 已标注 <c>[JsonIgnore]</c>，领域事件不会被序列化到 Redis JSON。
    /// 序列化前仍调用 <c>ClearDomainEvents()</c> 清理内存中的事件，避免事件在聚合生命周期内累积。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Lua 脚本：CAS 原子更新匿名购物车（P1-1 修复）。
    /// <para>
    /// KEYS[1] = cart:anon:{sessionId}
    /// ARGV[1] = expectedVersion（客户端期望的当前版本号，新创建购物车传入 0）
    /// ARGV[2] = newValue（序列化后的 Cart JSON）
    /// ARGV[3] = newVersion（保存成功后的新版本号，等于 expectedVersion + 1）
    /// ARGV[4] = ttl（TTL 秒数）
    /// </para>
    /// <para>
    /// 返回值：
    /// <list type="bullet">
    /// <item><c>1</c> = 保存成功（版本匹配或 key 不存在按首次创建处理）</item>
    /// <item><c>0</c> = 并发冲突（版本不匹配，未写入）</item>
    /// </list>
    /// </para>
    /// </summary>
    private const string CasSaveLuaScript = @"
local key = KEYS[1]
local expectedVersion = tonumber(ARGV[1])
local newValue = ARGV[2]
local newVersion = ARGV[3]
local ttl = tonumber(ARGV[4])

local currentVersion = redis.call('HGET', key, 'version')
if currentVersion == false then
    -- key 不存在，首次创建
    redis.call('HSET', key, 'payload', newValue, 'version', newVersion)
    redis.call('EXPIRE', key, ttl)
    return 1
end

if tonumber(currentVersion) ~= expectedVersion then
    -- 版本不匹配，并发冲突
    return 0
end

redis.call('HSET', key, 'payload', newValue, 'version', newVersion)
redis.call('EXPIRE', key, ttl)
return 1
";

    /// <summary>
    /// Lua 脚本：原子创建匿名购物车（仅当 key 不存在时写入）。
    /// <para>
    /// KEYS[1] = cart:anon:{sessionId}
    /// ARGV[1] = newValue（序列化后的 Cart JSON）
    /// ARGV[2] = ttl（TTL 秒数）
    /// </para>
    /// <para>
    /// 返回值：
    /// <list type="bullet">
    /// <item><c>1</c> = 创建成功（key 之前不存在，已写入 Hash 格式 version=1）</item>
    /// <item><c>0</c> = key 已存在（并发请求已创建），调用方应重新 GetAsync 读取</item>
    /// </list>
    /// </para>
    /// </summary>
    private const string TryCreateLuaScript = @"
local key = KEYS[1]
local newValue = ARGV[1]
local ttl = tonumber(ARGV[2])

if redis.call('EXISTS', key) == 1 then
    return 0
end

redis.call('HSET', key, 'payload', newValue, 'version', '1')
redis.call('EXPIRE', key, ttl)
return 1
";

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
    /// <remarks>
    /// P1-1：使用 <see cref="KeyTypeAsync"/> 判断存储格式，支持新的 Hash 格式与旧 String 格式向后兼容读取。
    /// Hash 格式读取后通过 <see cref="CartAggregate.MarkLoaded"/> 同步聚合 <see cref="CartAggregate.Revision"/>。
    /// </remarks>
    public async Task<CartAggregate?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            var keyType = await db.KeyTypeAsync(key);

            switch (keyType)
            {
                case RedisType.None:
                    return null;

                case RedisType.Hash:
                    {
                        // P1-1：Hash 格式读取 payload + version
                        var values = await db.HashGetAsync(
                            key,
                            new RedisValue[] { "payload", "version" });
                        var payloadValue = values[0];
                        if (!payloadValue.HasValue)
                        {
                            return null;
                        }

                        var cart = JsonSerializer.Deserialize<CartAggregate>((string)payloadValue!, JsonOptions);
                        if (cart is null)
                        {
                            return null;
                        }

                        var versionValue = values[1];
                        var version = versionValue.HasValue && int.TryParse((string)versionValue!, out var v) ? v : 0;
                        cart.MarkLoaded(version);
                        return cart;
                    }

                case RedisType.String:
                    {
                        // 向后兼容：迁移前 String 格式无 version 字段，按 0 处理
                        var value = await db.StringGetAsync(key);
                        if (!value.HasValue)
                        {
                            return null;
                        }

                        var cart = JsonSerializer.Deserialize<CartAggregate>((string)value!, JsonOptions);
                        if (cart is null)
                        {
                            return null;
                        }

                        cart.MarkLoaded(0);
                        return cart;
                    }

                default:
                    _logger.LogWarning(
                        "匿名购物车 key 类型异常 SessionId={SessionId} KeyType={KeyType}",
                        sessionId, keyType);
                    return null;
            }
        }
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "读取匿名购物车缓存失败 SessionId={SessionId}", sessionId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// P1-1：向后兼容重载，以聚合当前 <see cref="CartAggregate.Revision"/> 作为 expectedVersion 执行 CAS。
    /// 并发冲突时抛出 <see cref="CartConcurrencyException"/>，调用方应重新加载后重试。
    /// </remarks>
    public async Task SaveAsync(string sessionId, CartAggregate cart, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(cart);

        var expectedVersion = cart.Revision;
        var saved = await SaveAsync(sessionId, cart, expectedVersion, ct);

        if (!saved)
        {
            // CAS 冲突，读取 Redis 中的实际版本号用于诊断
            var actualVersion = await TryGetVersionAsync(sessionId);
            throw new CartConcurrencyException(
                $"匿名购物车并发冲突 SessionId={sessionId} ExpectedVersion={expectedVersion} ActualVersion={actualVersion}",
                expectedVersion,
                actualVersion);
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(string sessionId, CartAggregate cart, int expectedVersion, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(cart);
        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "expectedVersion 不可为负数");
        }

        try
        {
            // 序列化前清理领域事件，避免 Redis JSON 单调增长（_domainEvents 仅在 EF Core 落库路径
            // 由 SaveChangesWithOutboxAsync 清理，匿名购物车走 Redis 持久化路径需显式清理）
            cart.ClearDomainEvents();
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            var value = JsonSerializer.Serialize(cart, JsonOptions);
            var newVersion = expectedVersion + 1;
            var ttlSeconds = (int)Ttl.TotalSeconds;

            var result = (long)await db.ScriptEvaluateAsync(
                CasSaveLuaScript,
                new RedisKey[] { key },
                new RedisValue[]
                {
                    expectedVersion,
                    (RedisValue)value,
                    (RedisValue)newVersion.ToString(),
                    (RedisValue)ttlSeconds.ToString()
                });

            if (result == 1)
            {
                // CAS 成功，递增聚合 Revision 与 Redis Hash version 保持一致
                cart.MarkSaved(newVersion);
                return true;
            }

            // 版本不匹配，并发冲突，未写入
            _logger.LogInformation(
                "匿名购物车 CAS 冲突 SessionId={SessionId} ExpectedVersion={ExpectedVersion}",
                sessionId, expectedVersion);
            return false;
        }
        catch (Exception ex) when (ex is not CartInfrastructureException and not CartConcurrencyException)
        {
            _logger.LogError(ex, "写入匿名购物车缓存失败 SessionId={SessionId}", sessionId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// P1-1：使用 Lua 脚本原子创建（EXISTS + HSET + EXPIRE），仅当 key 不存在时写入 Hash 格式。
    /// 创建成功后聚合 <see cref="CartAggregate.Revision"/> 递增为 1。
    /// </remarks>
    public async Task<bool> TrySaveAsync(string sessionId, CartAggregate cart, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(cart);
        try
        {
            // 与 SaveAsync 对齐：序列化前清理领域事件
            cart.ClearDomainEvents();
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            var value = JsonSerializer.Serialize(cart, JsonOptions);
            var ttlSeconds = (int)Ttl.TotalSeconds;

            var result = (long)await db.ScriptEvaluateAsync(
                TryCreateLuaScript,
                new RedisKey[] { key },
                new RedisValue[]
                {
                    (RedisValue)value,
                    (RedisValue)ttlSeconds.ToString()
                });

            if (result == 1)
            {
                // 创建成功，同步聚合 Revision 为 1
                if (cart.Revision == 0)
                {
                    cart.MarkSaved(1);
                }
                return true;
            }

            return false;
        }
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "原子创建匿名购物车失败 SessionId={SessionId}", sessionId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
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
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "删除匿名购物车缓存失败 SessionId={SessionId}", sessionId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
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
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "刷新匿名购物车 TTL 失败 SessionId={SessionId}", sessionId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
        }
    }

    /// <summary>
    /// 旧的非原子保存实现（P1-1 修复前的 <c>SaveAsync</c>），作为 fallback 保留 1 个版本周期。
    /// <para>
    /// 直接 <c>StringSetAsync</c> 覆盖写，无版本检查，不防并发覆盖。
    /// 新代码应使用 <see cref="SaveAsync(string, CartAggregate, int, CancellationToken)"/> CAS 重载。
    /// </para>
    /// </summary>
    /// <param name="sessionId">会话标识。</param>
    /// <param name="cart">待保存的匿名购物车聚合。</param>
    /// <param name="ct">取消令牌。</param>
    [Obsolete("Use SaveAsync with CAS Lua script instead. 1 个版本周期后删除。")]
    public async Task SaveAsyncLegacy(string sessionId, CartAggregate cart, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(cart);
        try
        {
            cart.ClearDomainEvents();
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            var value = JsonSerializer.Serialize(cart, JsonOptions);
            await db.StringSetAsync(key, value, Ttl);
        }
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "[Legacy] 写入匿名购物车缓存失败 SessionId={SessionId}", sessionId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
        }
    }

    /// <summary>
    /// 读取 Redis Hash 中的 version 字段，用于 CAS 冲突时诊断实际版本号。
    /// key 不存在或非 Hash 格式时返回 0。
    /// </summary>
    private async Task<int> TryGetVersionAsync(string sessionId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            var keyType = await db.KeyTypeAsync(key);
            if (keyType != RedisType.Hash)
            {
                return 0;
            }

            var versionValue = await db.HashGetAsync(key, "version");
            if (!versionValue.HasValue)
            {
                return 0;
            }

            return int.TryParse((string)versionValue!, out var v) ? v : 0;
        }
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogWarning(ex, "读取匿名购物车版本号失败 SessionId={SessionId}", sessionId);
            return 0;
        }
    }

    private static string BuildKey(string sessionId) => $"cart:anon:{sessionId}";
}
