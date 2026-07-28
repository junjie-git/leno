using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>Redis 缓存监控抽象。</summary>
public interface IRedisCacheMonitor
{
    Task<RedisInfoDto> GetInfoAsync(CancellationToken ct = default);
    Task<List<KeyspaceDto>> GetKeyspacesAsync(CancellationToken ct = default);
    Task<PagedResult<RedisKeyDto>> ScanKeysAsync(int db, string pattern, string? type, int page, int pageSize, CancellationToken ct = default);
    Task<RedisKeyDetailDto?> GetKeyDetailAsync(string key, int db, CancellationToken ct = default);
    Task<bool> DeleteKeyAsync(string key, int db, CancellationToken ct = default);
}
