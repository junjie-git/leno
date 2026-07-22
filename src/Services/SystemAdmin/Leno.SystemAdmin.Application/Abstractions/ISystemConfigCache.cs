namespace Leno.SystemAdmin.Application.Abstractions;

/// <summary>
/// 系统配置缓存抽象，供应用层读侧加速。
/// 实现位于基础设施层（Redis），写操作后主动失效缓存避免脏读。
/// </summary>
public interface ISystemConfigCache
{
    /// <summary>按配置键读取缓存值，缓存缺失返回 null。</summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>写入配置缓存并刷新 TTL。</summary>
    Task SetAsync(string key, string value, CancellationToken ct = default);

    /// <summary>按配置键删除缓存。</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);
}
