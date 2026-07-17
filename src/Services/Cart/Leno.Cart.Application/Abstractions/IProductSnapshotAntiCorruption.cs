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
    /// <returns>SKU 快照；查询失败或不存在返回 null。</returns>
    Task<SkuSnapshotDto?> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default);
}
