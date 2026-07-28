using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application;

/// <summary>
/// 缓存监控应用服务接口。
/// 直连 Redis（通过 IRedisCacheMonitor 抽象），Redis 不可用时抛 503。
/// </summary>
public interface ICacheMonitorAppService
{
    /// <summary>获取 Redis INFO 概览。</summary>
    Task<RedisInfoDto> GetRedisInfoAsync(CancellationToken ct = default);

    /// <summary>获取 16 个 db 的 keyspace 信息。</summary>
    Task<List<KeyspaceDto>> GetKeyspacesAsync(CancellationToken ct = default);

    /// <summary>分页查询 key 列表（SCAN + TYPE 过滤）。</summary>
    Task<CacheKeyQueryResultDto> QueryKeysAsync(int db, string pattern, string? type, int page, int pageSize, CancellationToken ct = default);

    /// <summary>获取单个 key 详情（含 value，大 key 截断）。</summary>
    Task<RedisKeyDetailDto?> GetKeyDetailAsync(string key, int db, CancellationToken ct = default);

    /// <summary>删除 key，返回删除结果。</summary>
    Task<CacheKeyDeleteResultDto> DeleteKeyAsync(string key, int db, CancellationToken ct = default);
}
