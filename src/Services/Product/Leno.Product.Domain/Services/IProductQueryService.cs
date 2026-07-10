using Leno.SharedKernel.ValueObjects;

namespace Leno.Product.Domain.Services;

/// <summary>
/// 商品查询防腐层接口，供订单域等下游上下文查询 SKU 价格与库存。
/// 下游上下文不直接引用商品聚合，仅通过 SKU 标识查询以快照方式固化商品信息。
/// </summary>
public interface IProductQueryService
{
    /// <summary>
    /// 查询 SKU 当前销售价格。SKU 不存在返回 null，由调用方决定降级行为。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    Task<Money?> GetSkuPriceAsync(Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 查询 SKU 可售库存（库存基线可用量减预占）。
    /// 无基线记录返回 0。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    Task<int> GetSkuStockAsync(Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 批量查询 SKU 可售库存，供订单域结算前校验。
    /// 不存在的 SKU 不出现在结果中。
    /// </summary>
    /// <param name="skuIds">SKU 标识集合。</param>
    Task<IReadOnlyDictionary<Guid, int>> CheckSkusAvailableAsync(
        IReadOnlyCollection<Guid> skuIds,
        CancellationToken ct = default);
}
