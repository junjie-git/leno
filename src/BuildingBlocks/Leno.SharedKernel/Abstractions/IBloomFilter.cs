namespace Leno.SharedKernel.Abstractions;

/// <summary>
/// 布隆过滤器接口，用于缓存穿透防护。
/// 在查询缓存前快速判断 key 是否可能存在，过滤掉一定不存在的 key。
/// </summary>
public interface IBloomFilter
{
    /// <summary>
    /// 将 key 添加到布隆过滤器。
    /// </summary>
    /// <param name="key">要添加的键。</param>
    /// <param name="ct">取消令牌。</param>
    Task AddAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 检查 key 是否可能存在于布隆过滤器中。
    /// 返回 false 表示 key 一定不存在；返回 true 表示 key 可能存在（存在误判率）。
    /// </summary>
    /// <param name="key">要检查的键。</param>
    /// <param name="ct">取消令牌。</param>
    Task<bool> MightContainAsync(string key, CancellationToken ct = default);
}