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
}
