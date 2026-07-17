namespace Leno.Cart.Domain.Services;

/// <summary>
/// 购物车-SKU 反向索引服务，记录每个 SKU 出现在哪些购物车中。
/// 用于商品下架/上架/更新时快速定位受影响购物车，避免全量遍历。
/// 实现位于基础设施层（Redis Set），购物车域经此接口隔离存储细节。
/// </summary>
public interface ICartSkuIndexService
{
    /// <summary>将 (skuId, cartId) 加入索引。幂等：重复加入无副作用。</summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    /// <param name="cartId">购物车标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task AddAsync(Guid skuId, Guid cartId, CancellationToken ct = default);

    /// <summary>将 (skuId, cartId) 从索引移除。幂等：不存在的项移除无副作用。</summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    /// <param name="cartId">购物车标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task RemoveAsync(Guid skuId, Guid cartId, CancellationToken ct = default);

    /// <summary>查询包含指定 SKU 的所有购物车标识。</summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>购物车标识集合；无命中返回空集合。</returns>
    Task<List<Guid>> GetCartIdsBySkuAsync(Guid skuId, CancellationToken ct = default);
}
