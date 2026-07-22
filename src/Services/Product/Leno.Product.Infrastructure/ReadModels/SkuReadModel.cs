namespace Leno.Product.Infrastructure.ReadModels;

/// <summary>
/// SKU 读模型嵌套文档，作为 <see cref="ProductReadModel.Skus"/> 的元素。
/// 字段从 SPU 聚合内的 SKU 实体投影，供买家端商品详情页渲染 SKU 选择器。
/// 修复审计 #18：原 <see cref="ProductReadModel"/> 仅含 MinPrice/MaxPrice 价格区间，
/// 买家端商品详情页走 CQRS 读侧无法展示 SKU 列表，读侧 vs 写侧信息丢失。
/// </summary>
public sealed class SkuReadModel
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

    /// <summary>SKU 状态名称（Active/Inactive），与 <see cref="ProductReadModel.Status"/> 一致使用字符串。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>SKU 专属图，可空。</summary>
    public string? ImageUrl { get; init; }

    /// <summary>规格属性集合（如 颜色=红）。</summary>
    public IReadOnlyList<SkuSpecAttributeReadModel> SpecAttributes { get; init; } = Array.Empty<SkuSpecAttributeReadModel>();
}

/// <summary>
/// SKU 规格属性读模型嵌套文档，作为 <see cref="SkuReadModel.SpecAttributes"/> 的元素。
/// </summary>
public sealed class SkuSpecAttributeReadModel
{
    /// <summary>规格名（如 颜色）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>规格值（如 红）。</summary>
    public string Value { get; init; } = string.Empty;
}
