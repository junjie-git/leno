namespace Leno.Product.Application.Queries;

/// <summary>
/// 买家端商品详情结果（CQRS 读侧 Query Result）。
/// 字段来自 <c>ProductReadModel</c>：标题、副标题、价格区间、分类、店铺、状态、评分等。
/// </summary>
public sealed class ProductDetailResult
{
    public Guid ProductId { get; init; }

    public string Title { get; init; } = string.Empty;

    /// <summary>副标题/卖点（作为描述）。</summary>
    public string? Subtitle { get; init; }

    public string MainImageUrl { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }

    public Guid? BrandId { get; init; }

    public Guid ShopId { get; init; }

    /// <summary>商品状态名称（OnSale 等）。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>规格维度名集合（如颜色、尺寸）。</summary>
    public IReadOnlyList<string> Specs { get; init; } = Array.Empty<string>();

    /// <summary>最低 SKU 价格（价格区间下界）。</summary>
    public decimal MinPrice { get; init; }

    /// <summary>最高 SKU 价格（价格区间上界）。</summary>
    public decimal MaxPrice { get; init; }

    public string Currency { get; init; } = "CNY";

    /// <summary>
    /// SKU 列表。修复审计 #18：原读侧结果仅含价格区间，买家端详情页无法渲染 SKU 选择器，
    /// 读侧 vs 写侧信息丢失。现由 <c>ProductReadModelAccessor</c> 从 <c>ProductReadModel.Skus</c> 映射。
    /// </summary>
    public IReadOnlyList<SkuDetailResult> Skus { get; init; } = Array.Empty<SkuDetailResult>();

    /// <summary>加权平均评分。</summary>
    public double Score { get; init; }

    /// <summary>可见评价总数。</summary>
    public int ReviewCount { get; init; }

    /// <summary>读模型索引时间（UTC），用于排查同步延迟。</summary>
    public DateTime IndexedAt { get; init; }
}

/// <summary>
/// 买家端商品详情 SKU 结果（CQRS 读侧 Query Result）。
/// 字段来自 <c>SkuReadModel</c>，与 <see cref="ProductDetailResult"/> 同生命周期，
/// 供买家端商品详情页渲染 SKU 选择器。
/// </summary>
public sealed class SkuDetailResult
{
    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>商家自定义编码。</summary>
    public string SkuCode { get; init; } = string.Empty;

    /// <summary>销售价格。</summary>
    public decimal Price { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>可售库存基线。</summary>
    public int StockQty { get; init; }

    /// <summary>SKU 状态名称（Active/Inactive）。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>SKU 专属图，可空。</summary>
    public string? ImageUrl { get; init; }

    /// <summary>规格属性集合（如 颜色=红）。</summary>
    public IReadOnlyList<SkuSpecAttributeResult> SpecAttributes { get; init; } = Array.Empty<SkuSpecAttributeResult>();
}

/// <summary>
/// 买家端商品详情 SKU 规格属性结果。
/// </summary>
public sealed class SkuSpecAttributeResult
{
    /// <summary>规格名（如 颜色）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>规格值（如 红）。</summary>
    public string Value { get; init; } = string.Empty;
}
