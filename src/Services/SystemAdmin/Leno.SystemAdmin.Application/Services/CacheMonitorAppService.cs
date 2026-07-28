using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// 缓存监控应用服务实现。
/// 委托 IRedisCacheMonitor 域服务抽象完成 Redis 操作；db 越界与 pattern 长度校验在应用层完成。
/// Redis 不可用时抛 SystemAdminDomainException(code CACHE_REDIS_UNAVAILABLE) 由中间件映射 503。
/// </summary>
public sealed class CacheMonitorAppService : ICacheMonitorAppService
{
    private const int MaxDbIndex = 15;
    private const int MaxPatternLength = 256;

    private readonly IRedisCacheMonitor _redisCacheMonitor;
    private readonly ILogger<CacheMonitorAppService> _logger;

    public CacheMonitorAppService(
        IRedisCacheMonitor redisCacheMonitor,
        ILogger<CacheMonitorAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(redisCacheMonitor);
        ArgumentNullException.ThrowIfNull(logger);
        _redisCacheMonitor = redisCacheMonitor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RedisInfoDto> GetRedisInfoAsync(CancellationToken ct = default)
    {
        try
        {
            return await _redisCacheMonitor.GetInfoAsync(ct);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis 不可用，缓存信息查询失败");
            throw new SystemAdminDomainException("Redis 暂时不可用", "CACHE_REDIS_UNAVAILABLE");
        }
    }

    /// <inheritdoc />
    public async Task<List<KeyspaceDto>> GetKeyspacesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _redisCacheMonitor.GetKeyspacesAsync(ct);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis 不可用，keyspace 查询失败");
            throw new SystemAdminDomainException("Redis 暂时不可用", "CACHE_REDIS_UNAVAILABLE");
        }
    }

    /// <inheritdoc />
    public async Task<CacheKeyQueryResultDto> QueryKeysAsync(int db, string pattern, string? type, int page, int pageSize, CancellationToken ct = default)
    {
        ValidateDb(db);
        ValidatePattern(pattern);
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        PagedResult<RedisKeyDto> result;
        try
        {
            result = await _redisCacheMonitor.ScanKeysAsync(db, pattern, type, page, pageSize, ct);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis 不可用，key 查询失败");
            throw new SystemAdminDomainException("Redis 暂时不可用", "CACHE_REDIS_UNAVAILABLE");
        }

        return new CacheKeyQueryResultDto
        {
            Items = result.Items,
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<RedisKeyDetailDto?> GetKeyDetailAsync(string key, int db, CancellationToken ct = default)
    {
        ValidateDb(db);
        ValidateKey(key);

        try
        {
            return await _redisCacheMonitor.GetKeyDetailAsync(key, db, ct);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis 不可用，key 详情查询失败 Key={Key}", key);
            throw new SystemAdminDomainException("Redis 暂时不可用", "CACHE_REDIS_UNAVAILABLE");
        }
    }

    /// <inheritdoc />
    public async Task<CacheKeyDeleteResultDto> DeleteKeyAsync(string key, int db, CancellationToken ct = default)
    {
        ValidateDb(db);
        ValidateKey(key);

        bool deleted;
        try
        {
            deleted = await _redisCacheMonitor.DeleteKeyAsync(key, db, ct);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis 不可用，key 删除失败 Key={Key}", key);
            throw new SystemAdminDomainException("Redis 暂时不可用", "CACHE_REDIS_UNAVAILABLE");
        }

        _logger.LogWarning("缓存 key 已删除 Key={Key} Db={Db} Deleted={Deleted}", key, db, deleted);
        return new CacheKeyDeleteResultDto { Deleted = deleted, Key = key };
    }

    private static void ValidateDb(int db)
    {
        if (db < 0 || db > MaxDbIndex)
        {
            throw new SystemAdminDomainException($"db 越界，必须在 0-{MaxDbIndex} 范围", "CACHE_DB_OUT_OF_RANGE");
        }
    }

    private static void ValidatePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new SystemAdminDomainException("pattern 不可为空", "CACHE_PATTERN_EMPTY");
        }
        if (pattern.Length > MaxPatternLength)
        {
            throw new SystemAdminDomainException($"pattern 长度不可超过 {MaxPatternLength} 字符", "CACHE_PATTERN_LENGTH");
        }
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new SystemAdminDomainException("key 不可为空", "CACHE_KEY_EMPTY");
        }
        if (key.Length > 1024)
        {
            throw new SystemAdminDomainException("key 长度不可超过 1024 字符", "CACHE_KEY_LENGTH");
        }
    }
}
