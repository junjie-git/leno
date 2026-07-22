namespace Leno.SystemAdmin.Application.Abstractions;

/// <summary>
/// 系统配置缓存抽象，定义于应用层，由基础设施层（Redis 实现）实现。
/// 应用层仅依赖此接口以失效写操作后的缓存，避免直接依赖 Infrastructure 与 Redis 实现细节。
/// </summary>
public interface ISystemConfigCache
{
    /// <summary>按配置键删除缓存。</summary>
    /// <param name="key">配置键。</param>
    /// <param name="ct">取消令牌。</param>
    Task RemoveAsync(string key, CancellationToken ct = default);
}
