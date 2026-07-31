namespace Leno.Product.Application;

/// <summary>
/// 商品域内部查询服务，供其他微服务通过 HTTP 调用获取 SKU 信息。
/// </summary>
public interface IProductInternalQueryService
{
    /// <summary>按 SKU 标识查询其概要信息（价格、可售性、标题、主图、卖家），不存在返回 null。</summary>
    Task<SkuInfoResultDto?> GetSkuInfoAsync(Guid skuId, CancellationToken ct = default);

    /// <summary>批量查询 SKU 概要信息，跳过不存在的 SKU。</summary>
    Task<List<SkuInfoResultDto>> GetSkuInfosBatchAsync(List<Guid> skuIds, CancellationToken ct = default);

    /// <summary>
    /// 按 SKU 标识查询库存基线（可用 + 预占），权威值取自 StockBaseline 聚合，不存在返回 null。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<SkuStockResultDto?> GetSkuStockAsync(Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 按 SPU 标识查询商品详情（含 SKU 集合），不存在返回 null。
    /// </summary>
    /// <param name="spuId">SPU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<SpuDetailResultDto?> GetSpuDetailAsync(Guid spuId, CancellationToken ct = default);

    /// <summary>
    /// 按店铺标识查询低库存 SKU（StockQty &lt; threshold），返回按库存升序排列的列表。
    /// 数据来自 SPU 聚合内 SKU 实体的 StockQty 字段。
    /// </summary>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="threshold">低库存阈值，StockQty 小于此值视为低库存。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>低库存 SKU 列表，无命中返回空列表。</returns>
    Task<List<LowStockSkuDto>> GetLowStockByShopAsync(Guid shopId, int threshold, CancellationToken ct = default);
}
