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

    /// <summary>加权平均评分。</summary>
    public double Score { get; init; }

    /// <summary>可见评价总数。</summary>
    public int ReviewCount { get; init; }

    /// <summary>读模型索引时间（UTC），用于排查同步延迟。</summary>
    public DateTime IndexedAt { get; init; }
}
