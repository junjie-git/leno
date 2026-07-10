namespace Leno.Cart.Domain.Services;

/// <summary>
/// 购物车价格防腐层接口，供购物车预览时查询商品域实时 SKU 价格与可售状态。
/// 实现位于基础设施层，通过商品域 API 或防腐层调用，购物车域不直接依赖商品域领域模型。
/// </summary>
public interface ICartPriceService
{
    /// <summary>
    /// 批量查询 SKU 价格与可售状态。
    /// </summary>
    /// <param name="skuIds">待查询的 SKU 标识集合。</param>
    /// <returns>SKU 价格与可售状态快照集合。</returns>
    Task<IReadOnlyList<SkuPriceSnapshot>> GetSkuPricesAsync(IEnumerable<Guid> skuIds, CancellationToken ct = default);
}

/// <summary>
/// SKU 价格与可售状态快照，由商品域防腐层返回。
/// </summary>
public sealed class SkuPriceSnapshot
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>SKU 单价。</summary>
    public decimal Price { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>是否可售（在售且有库存）。</summary>
    public bool Available { get; init; }

    /// <summary>商品标题（用于购物车展示）。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>主图 URL（用于购物车展示）。</summary>
    public string MainImageUrl { get; init; } = string.Empty;

    /// <summary>所属卖家（店铺）标识。</summary>
    public Guid SellerId { get; init; }
}
