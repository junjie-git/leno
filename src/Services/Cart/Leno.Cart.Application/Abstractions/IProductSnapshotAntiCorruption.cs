using Leno.Cart.Application.DTOs;

namespace Leno.Cart.Application.Abstractions;

/// <summary>
/// 商品域快照防腐层，查询商品域获取 SKU 最新展示信息。
/// 购物车域不直接依赖商品域领域模型，经此防腐层隔离上下文。
/// </summary>
public interface IProductSnapshotAntiCorruption
{
    /// <summary>
    /// 查询 SKU 当前快照（标题、图片、价格、在售状态）。
    /// </summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>SKU 快照。</returns>
    /// <exception cref="Leno.Infrastructure.AntiCorruption.AntiCorruptionException">
    /// 查询失败抛 PRODUCT_UNAVAILABLE；SKU 不存在抛 PRODUCT_REMOTE_FAILED。
    /// </exception>
    Task<SkuSnapshotDto> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 批量查询 SKU 当前快照（标题、图片、价格、在售状态）。
    /// 单次 ACL 调用替代 N 次 <see cref="GetSkuSnapshotAsync"/>，避免商品更新事件触发 N 次 HTTP。
    /// </summary>
    /// <param name="skuIds">商品 SKU 标识集合。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>命中的 SKU 快照集合；未命中的 SKU 不在结果中（调用方按 SkuId 字典查表）。</returns>
    /// <exception cref="Leno.Infrastructure.AntiCorruption.AntiCorruptionException">
    /// 批量查询失败抛 PRODUCT_UNAVAILABLE。
    /// </exception>
    Task<IReadOnlyList<SkuSnapshotDto>> GetSkuSnapshotsAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default);
}
