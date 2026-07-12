namespace Leno.Product.Domain.Services;

/// <summary>
/// 商品唯一性校验器接口，定义在领域层，由基础设施层实现。
/// 用于校验 SKU 编码全局唯一性、商品标题同店铺唯一性。
/// </summary>
public interface IProductUniquenessChecker
{
    /// <summary>
    /// 校验 SKU 编码全局唯一。编辑场景可通过 excludeProductId 排除当前商品。
    /// </summary>
    /// <param name="skuCode">SKU 编码。</param>
    /// <param name="excludeProductId">排除的商品标识，编辑场景传入当前商品 ID。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true 表示唯一，false 表示已存在。</returns>
    Task<bool> IsSkuCodeUniqueAsync(string skuCode, Guid? excludeProductId = null, CancellationToken ct = default);

    /// <summary>
    /// 校验商品标题同店铺唯一。编辑场景可通过 excludeProductId 排除当前商品。
    /// </summary>
    /// <param name="title">商品标题。</param>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="excludeProductId">排除的商品标识，编辑场景传入当前商品 ID。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true 表示唯一，false 表示已存在。</returns>
    Task<bool> IsTitleUniqueInShopAsync(string title, Guid shopId, Guid? excludeProductId = null, CancellationToken ct = default);
}