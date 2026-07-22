namespace Leno.SystemAdmin.Application.Abstractions;

/// <summary>
/// 特性开关缓存抽象，供应用层读侧加速。
/// 实现位于基础设施层（Redis），写操作后主动失效缓存避免脏读。
/// </summary>
public interface IFeatureFlagCache
{
    /// <summary>按开关键读取缓存值，缓存缺失返回 null。</summary>
    Task<string?> GetAsync(string flagKey, CancellationToken ct = default);

    /// <summary>写入开关缓存并刷新 TTL。</summary>
    Task SetAsync(string flagKey, string value, CancellationToken ct = default);

    /// <summary>按开关键删除缓存。</summary>
    Task RemoveAsync(string flagKey, CancellationToken ct = default);
}
